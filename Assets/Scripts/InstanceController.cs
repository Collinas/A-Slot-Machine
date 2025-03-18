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

public class InstanceController : MonoBehaviour
{
    [Header("Prefabs e Containers")]
    [SerializeField] private GameObject instancePrefab;
    [SerializeField] private Transform contentContainer;
    [SerializeField] private GameObject textInfoPrefab;
    [SerializeField] private Transform logContainer;
    [SerializeField] private Text log001CounterText; // Campo para exibir o contador de LOG001

    private TcpListener server;
    private Thread serverThread;
    private bool isServerRunning = false;
    private readonly List<Process> instanceProcesses = new();
    private readonly List<int> instanceIds = new();
    private int log001Counter = 300; // Inicia o contador em 300
    private readonly List<TcpClient> connectedClients = new(); // Lista de clientes conectados

    private void Start()
    {
        Application.runInBackground = true;
        if (contentContainer == null) UnityEngine.Debug.LogWarning("O Content container não foi configurado no inspetor.");

        string[] args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && args[1] == "SlotScene") SceneManager.LoadScene("SlotScene");

        StartServer();
        UpdateLog001CounterText(); // Inicializa o texto do contador
    }

    private void StartServer()
    {
        isServerRunning = true;
        serverThread = new(RunServer);
        serverThread.Start();
    }

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
                    // Incrementa o contador
                    log001Counter++;

                    // 1/200 chance de resetar o contador para 300
                    if (UnityEngine.Random.Range(0, 200) == 0) // 1 em 200 chances
                    {
                        // Envia o valor atual do contador para a instância que causou o reset
                        SendMessageToClient(client, $"LogCounterReset:{log001Counter}");

                        // Reseta o contador para 300
                        log001Counter = 300;
                    }

                    // Atualiza o texto do contador
                    UpdateLog001CounterText();

                    // Envia o novo valor do contador para todos os clientes
                    SendMessageToAllClients($"LogCounterUpdate:{log001Counter}");
                }
            }
        }

        // Remove o cliente da lista quando a conexão é fechada
        connectedClients.Remove(client);
        client.Close();
    }

    private void AddLogToUI(string instanceId, string logMessage)
    {
        GameObject newLog = Instantiate(textInfoPrefab, logContainer);
        Text logText = newLog.GetComponent<Text>();
        if (logText != null) logText.text = $"[{instanceId}] {logMessage}";
    }

    private void UpdateLog001CounterText()
    {
        if (log001CounterText != null)
        {
            log001CounterText.text = $"{log001Counter}";
        }
    }

    private void SendMessageToAllClients(string message)
    {
        foreach (var client in connectedClients.ToList()) // Usar ToList() para evitar modificações durante a iteração
        {
            SendMessageToClient(client, message);
        }
    }

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

    private void OnInstanceProcessExited(Process process, int instanceId, GameObject instanceUI)
    {
        instanceProcesses.Remove(process);
        instanceIds.Remove(instanceId);
        Destroy(instanceUI);
        UnityEngine.Debug.Log($"Instância {instanceId:0000} foi fechada manualmente.");
        AddLogToUI(instanceId.ToString(), "Fechada manualmente.");
    }

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
}