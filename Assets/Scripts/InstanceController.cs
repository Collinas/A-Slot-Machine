using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// [Main] Controlador principal que gerencia as instâncias do jogo e a comunicação com os clientes.
/// </summary>
public class InstanceController : MonoBehaviour
{
    [Header("Prefabs e Containers")]
    [Tooltip("Prefab da instância do jogo")]
    [SerializeField] private GameObject instancePrefab;
    [Tooltip("Container para as instâncias")]
    [SerializeField] private Transform contentContainer;
    [Tooltip("Prefab para exibir logs na UI")]
    [SerializeField] private GameObject textInfoPrefab;
    [Tooltip("Container para os logs")]
    [SerializeField] private Transform logContainer;
    [Tooltip("Campo para exibir o contador Jackpot")]
    [SerializeField] private Text log001CounterText;

    private TcpListener server; // Servidor TCP para comunicação com os clientes
    private Thread serverThread; // Thread para executar o servidor
    private bool isServerRunning = false; // Flag para controlar o estado do servidor
    private readonly List<Process> instanceProcesses = new(); // Lista de processos das instâncias
    private readonly List<int> instanceIds = new(); // Lista de IDs das instâncias
    private float log001Counter = 300; // Contador do Jackpot, inicia em 300
    private readonly List<TcpClient> connectedClients = new(); // Lista de clientes conectados

    ///////////////////////////////////////////// InitializeInstance.cs

    /// <summary>
    /// Inicializa o controlador, configura a resolução da tela e inicia o servidor.
    /// </summary>
    private void Start()
    {
        Application.runInBackground = true;
        if (contentContainer == null) UnityEngine.Debug.LogWarning("O Content container não foi configurado no inspetor.");

        // Define a resolução inicial da janela para 700x500
        Screen.SetResolution(700, 500, false);

        string[] args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && args[1] == "SlotScene") SceneManager.LoadScene("SlotScene");

        StartServer();
        UpdateLog001CounterText(); // Inicializa o texto do contador
    }

    /// <summary>
    /// Inicia o servidor TCP em uma nova thread.
    /// </summary>
    private void StartServer()
    {
        isServerRunning = true;
        serverThread = new(RunServer);
        serverThread.Start();
    }

    /// <summary>
    /// Método executado pela thread do servidor para aceitar conexões de clientes.
    /// </summary>
    private void RunServer()
    {
        server = new(System.Net.IPAddress.Any, 12345);
        server.Start();

        while (isServerRunning)
        {
            if (server.Pending())
            {
                TcpClient newClient = server.AcceptTcpClient();
                connectedClients.Add(newClient); // Adiciona o novo cliente à lista
                Thread clientThread = new(() => HandleClient(newClient));
                clientThread.Start();

                // Envia o valor atual do contador para o novo cliente
                SendMessageToClient(newClient, $"LogCounterUpdate:{log001Counter}");
            }
        }
    }

    ///////////////////////////////////////////// InstanceToSlotsCommunication.cs

    /// <summary>
    /// Manipula a comunicação com um cliente conectado.
    /// </summary>
    /// <param name="client">Cliente TCP conectado.</param>
    private void HandleClient(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[1024];
        int bytesRead;

        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
        {
            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            UnityEngine.Debug.Log("Mensagem recebida: " + message);

            if (message.StartsWith("SlotControllerAtivo:"))
            {
                // Atualiza a interface se necessário
            }
            else if (message.StartsWith("Log:"))
            {
                string[] parts = message.Split(':');
                if (parts.Length >= 3) AddLogToUI(parts[1], parts[2]);

                // Verifica se a mensagem é LOG001
                if (parts[2].StartsWith("LOG001"))
                {
                    // Incrementa o contador por 0.01
                    log001Counter += 0.01f;

                    // 1/200 chance de resetar o contador para 300
                    if (UnityEngine.Random.Range(0, 200) == 0) // 1 em 200 chances
                    {
                        // Envia o valor atual do contador para a instância que causou o reset
                        SendMessageToClient(client, $"LogCounterReset:{log001Counter:F2}");

                        // Reseta o contador para 300
                        log001Counter = 300f;
                    }

                    // Atualiza o texto do contador
                    UpdateLog001CounterText();

                    // Envia o novo valor do contador para todos os clientes
                    SendMessageToAllClients($"LogCounterUpdate:{log001Counter:F2}");
                }
            }
        }

        // Remove o cliente da lista quando a conexão é fechada
        connectedClients.Remove(client);
        client.Close();
    }

    /// <summary>
    /// Envia uma mensagem para todos os clientes conectados.
    /// </summary>
    /// <param name="message">Mensagem a ser enviada.</param>
    private void SendMessageToAllClients(string message)
    {
        foreach (var client in connectedClients.ToList()) // Usar ToList() para evitar modificações durante a iteração
        {
            SendMessageToClient(client, message);
        }
    }

    /// <summary>
    /// Envia uma mensagem para um cliente específico.
    /// </summary>
    /// <param name="client">Cliente TCP.</param>
    /// <param name="message">Mensagem a ser enviada.</param>
    private void SendMessageToClient(TcpClient client, string message)
    {
        if (client != null && client.Connected)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                NetworkStream stream = client.GetStream();
                stream.Write(data, 0, data.Length);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Erro ao enviar mensagem para o cliente: " + e.Message);
                connectedClients.Remove(client); // Remove o cliente se houver erro
            }
        }
    }

    ///////////////////////////////////////////// InstanceSlotsController.cs

    /// <summary>
    /// Adiciona uma nova instância do jogo.
    /// </summary>
    public void AddNextInstance()
    {
        int instanceId = GenerateUniqueInstanceId();
        GameObject newInstance = Instantiate(instancePrefab, contentContainer);
        newInstance.name = $"Instance_{instanceId:0000}";

        Text instanceText = newInstance.GetComponentInChildren<Text>();
        if (instanceText != null)
        {
            instanceText.text = $"{instanceId:0000} - Aberta";
        }

        Button removeButton = newInstance.GetComponentInChildren<Button>();
        if (removeButton != null)
        {
            removeButton.onClick.AddListener(() => RemoveInstance(newInstance, instanceId));
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = Application.dataPath + "/../" + Application.productName + ".exe",
            Arguments = $"SlotScene {instanceId}"
        };

        Process newProcess = Process.Start(startInfo);
        if (newProcess != null)
        {
            instanceProcesses.Add(newProcess);
            newProcess.EnableRaisingEvents = true;
            newProcess.Exited += (sender, e) => OnInstanceProcessExited(newProcess, instanceId, newInstance);
        }

        StartCoroutine(WaitForSlotControllerConfirmation(instanceId, newInstance));
    }


    /// <summary>
    /// Gera um ID único para uma nova instância.
    /// </summary>
    /// <returns>ID único gerado.</returns>
    private int GenerateUniqueInstanceId()
    {
        int newId;
        do
        {
            newId = UnityEngine.Random.Range(0, 10000);
        } while (instanceIds.Contains(newId));

        instanceIds.Add(newId);
        return newId;
    }

    /// <summary>
    /// Aguarda a confirmação de abertura de uma instância.
    /// </summary>
    /// <param name="instanceId">ID da instância.</param>
    /// <param name="newInstance">Objeto UI da nova instância.</param>
    /// <returns>Coroutine.</returns>
    private IEnumerator WaitForSlotControllerConfirmation(int instanceId, GameObject newInstance)
    {
        float timeout = 10f;
        float startTime = Time.time;

        while (Time.time - startTime < timeout && !instanceIds.Contains(instanceId))
        {
            yield return null;
        }

        if (!instanceIds.Contains(instanceId))
        {
            RemoveInstance(newInstance, instanceId);
            UnityEngine.Debug.LogWarning($"Instância {instanceId:0000} não confirmou a abertura e foi removida.");
        }
    }

    /// <summary>
    /// Remove uma instância do jogo.
    /// </summary>
    /// <param name="instance">Objeto UI da instância.</param>
    /// <param name="instanceId">ID da instância.</param>
    private void RemoveInstance(GameObject instance, int instanceId)
    {
        Destroy(instance);
        instanceIds.Remove(instanceId);

        Process processToClose = instanceProcesses.FirstOrDefault(p => p.Id == instanceId);
        if (processToClose != null && !processToClose.HasExited)
        {
            processToClose.CloseMainWindow();
            processToClose.Close();
            instanceProcesses.Remove(processToClose);
        }

        SendMessageToAllClients($"Instância {instanceId:0000} fechada.");
    }

    /// <summary>
    /// Método chamado quando o processo de uma instância é encerrado.
    /// </summary>
    /// <param name="process">Processo da instância.</param>
    /// <param name="instanceId">ID da instância.</param>
    /// <param name="instanceUI">Objeto UI da instância.</param>
    private void OnInstanceProcessExited(Process process, int instanceId, GameObject instanceUI)
    {
        instanceProcesses.Remove(process);
        instanceIds.Remove(instanceId);
        Destroy(instanceUI);
        UnityEngine.Debug.Log($"Instância {instanceId:0000} foi fechada manualmente.");
        AddLogToUI(instanceId.ToString(), "Fechada manualmente.");
    }

    ///////////////////////////////////////////// InstanceLogs.cs

    /// <summary>
    /// Adiciona uma mensagem de log à interface do usuário.
    /// </summary>
    /// <param name="instanceId">ID da instância que gerou o log.</param>
    /// <param name="logMessage">Mensagem de log.</param>
    private void AddLogToUI(string instanceId, string logMessage)
    {
        GameObject newLog = Instantiate(textInfoPrefab, logContainer);
        Text logText = newLog.GetComponent<Text>();
        if (logText != null) logText.text = $"[{instanceId}] {logMessage}";
    }

    /// <summary>
    /// Atualiza o texto do contador de Jackpot na interface do usuário.
    /// </summary>
    private void UpdateLog001CounterText()
    {
        if (log001CounterText != null)
        {
            log001CounterText.text = $"{log001Counter:F2}"; // Format to 2 decimal places
        }
    }

    ///////////////////////////////////////////// InstanceQuit.cs

    /// <summary>
    /// Método chamado quando a aplicação é encerrada. Fecha o servidor e todos os processos das instâncias.
    /// </summary>
    private void OnApplicationQuit()
    {
        isServerRunning = false;
        server?.Stop();

        // Fecha todas as conexões com os clientes
        foreach (var client in connectedClients)
        {
            client.Close();
        }

        // Fecha todos os processos das instâncias
        foreach (Process process in instanceProcesses)
        {
            if (!process.HasExited)
            {
                process.CloseMainWindow();
                process.Close();
            }
        }
    }
}