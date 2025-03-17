using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System;
using System.Diagnostics;
using System.Linq;
using System.Collections;

public class InstanceController : MonoBehaviour
{
    [SerializeField]
    private GameObject instancePrefab; // Prefab da instância que contém Text e Button

    [SerializeField]
    private Transform contentContainer; // Container com VerticalLayoutGroup para exibir as instâncias

    [SerializeField]
    private GameObject textInfoPrefab; // Prefab para exibir as mensagens

    [SerializeField]
    private Transform logContainer; // Container com VerticalLayoutGroup para exibir os logs

    // Variáveis para comunicação via socket
    private TcpClient client;
    private TcpListener server;
    private Thread serverThread;
    private bool isServerRunning = false;

    // Lista para rastrear os processos das instâncias abertas
    private List<Process> instanceProcesses = new List<Process>();

    // Lista para rastrear os IDs das instâncias
    private List<int> instanceIds = new List<int>();

    // Variável para armazenar o valor global do Jackpot
    private float globalJackpot = 300f;

    void Start()
    {
        Application.runInBackground = true; // Permite que o aplicativo rode em segundo plano

        // Verifica se o Content foi configurado no inspetor
        if (contentContainer == null)
        {
            UnityEngine.Debug.LogWarning("O Content container não foi configurado no inspetor.");
        }

        // Verifica se a instância foi aberta com o argumento para carregar a cena "SlotScene"
        string[] args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && args[1] == "SlotScene")
        {
            SceneManager.LoadScene("SlotScene");
        }

        // Inicia o servidor de comunicação se for a primeira instância
        StartServer();
    }

    // Método para iniciar o servidor de comunicação
    private void StartServer()
    {
        isServerRunning = true;
        serverThread = new Thread(new ThreadStart(RunServer));
        serverThread.Start();
    }

    // Método que roda o servidor em uma thread separada
    private void RunServer()
    {
        server = new TcpListener(System.Net.IPAddress.Any, 12345); // Escuta na porta 12345
        server.Start();

        while (isServerRunning)
        {
            if (server.Pending())
            {
                client = server.AcceptTcpClient();
                UnityEngine.Debug.Log("Cliente conectado!");
                Thread clientThread = new Thread(new ThreadStart(HandleClient));
                clientThread.Start();
            }
        }
    }

    // Método para lidar com a comunicação do cliente
    private void HandleClient()
    {
        NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[1024];
        int bytesRead;

        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
        {
            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            UnityEngine.Debug.Log("Mensagem recebida: " + message);

            // Processa a mensagem recebida
            if (message.StartsWith("SlotControllerAtivo:"))
            {
                string[] parts = message.Split(':');
                if (parts.Length >= 2)
                {
                    string instanceId = parts[1]; // Identificador da instância
                    UpdateInstancePrefabText(instanceId); // Atualiza o texto da instancePrefab
                }
            }
            else if (message.StartsWith("Log:"))
            {
                string[] parts = message.Split(':');
                if (parts.Length >= 3)
                {
                    string instanceId = parts[1]; // Identificador da instância
                    string logMessage = parts[2]; // Mensagem do log
                    AddLogToUI(instanceId, logMessage);
                }
            }
            else if (message.StartsWith("JackpotWon:"))
            {
                // Quando uma instância ganha o Jackpot, resetamos o valor global
                ResetGlobalJackpot();
            }
            else if (message.StartsWith("RequestJackpot"))
            {
                // Envia o valor atual do Jackpot para a instância que solicitou
                SendMessageToClient($"UpdateJackpot:{globalJackpot}");
            }
        }

        client.Close();
    }

    // Método para enviar mensagem para o cliente
    private void SendMessageToClient(string message)
    {
        if (client == null || !client.Connected)
        {
            UnityEngine.Debug.LogWarning("Conexão com o cliente não está ativa.");
            return;
        }

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            NetworkStream stream = client.GetStream();
            stream.Write(data, 0, data.Length);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Erro ao enviar mensagem para o cliente: " + e.Message);
        }
    }

    // Método para atualizar o Jackpot globalmente
    public void UpdateGlobalJackpot(float newValue)
    {
        globalJackpot = newValue;
        NotifyAllInstances($"UpdateJackpot:{globalJackpot}");
    }

    // Método para resetar o Jackpot globalmente
    public void ResetGlobalJackpot()
    {
        globalJackpot = 300f;
        NotifyAllInstances($"ResetJackpot:{globalJackpot}");
    }

    // Método para notificar todas as instâncias sobre mudanças no Jackpot
    private void NotifyAllInstances(string message)
    {
        foreach (var process in instanceProcesses)
        {
            if (!process.HasExited)
            {
                SendMessageToServer(message);
            }
        }
    }

    // Método para atualizar o texto da instancePrefab com o ID recebido
    private void UpdateInstancePrefabText(string instanceId)
    {
        // Encontra a instância correspondente na lista de instâncias
        foreach (Transform child in contentContainer)
        {
            Text instanceText = child.GetComponentInChildren<Text>();
            if (instanceText != null && instanceText.text.Contains("Aberta"))
            {
                // Atualiza o texto com o ID recebido
                instanceText.text = $"{instanceId} - Aberta";
                break;
            }
        }
    }

    // Método para adicionar logs à interface do usuário
    private void AddLogToUI(string instanceId, string logMessage)
    {
        // Cria uma nova instância do prefab TextInfo
        GameObject newLog = Instantiate(textInfoPrefab, logContainer);
        Text logText = newLog.GetComponent<Text>();
        if (logText != null)
        {
            logText.text = $"[{instanceId}] {logMessage}"; // Exibe o ID da instância e a mensagem
        }
    }

    // Método para enviar mensagem para o servidor
    private void SendMessageToServer(string message)
    {
        if (client == null)
        {
            client = new TcpClient("127.0.0.1", 12345); // Conecta ao servidor na porta 12345
        }

        NetworkStream stream = client.GetStream();
        byte[] data = Encoding.UTF8.GetBytes(message);
        stream.Write(data, 0, data.Length);
    }

    // Método para gerar um ID único para a instância
    private int GenerateUniqueInstanceId()
    {
        int newId;
        do
        {
            newId = UnityEngine.Random.Range(0, 10000); // Gera um número entre 0000 e 9999
        } while (instanceIds.Contains(newId)); // Garante que o ID seja único

        instanceIds.Add(newId); // Adiciona o ID à lista de IDs existentes
        return newId;
    }

    // Método para adicionar uma nova instância dinamicamente
    public void AddNextInstance()
    {
        // Gera um ID único para a nova instância
        int instanceId = GenerateUniqueInstanceId();

        // Instancia o prefab da instância como filho do Content
        GameObject newInstance = Instantiate(instancePrefab, contentContainer);
        newInstance.name = $"Instance_{instanceId:0000}"; // Define o nome da instância no Inspector

        // Configura o texto da instância
        Text instanceText = newInstance.GetComponentInChildren<Text>();
        if (instanceText != null)
        {
            instanceText.text = $"{instanceId:0000} - Aberta";
        }

        // Configura o botão da instância para remover a instância
        Button removeButton = newInstance.GetComponentInChildren<Button>();
        if (removeButton != null)
        {
            removeButton.onClick.AddListener(() => RemoveInstance(newInstance, instanceId));
        }

        // Abre uma nova instância da build
        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = Application.dataPath + "/../" + Application.productName + ".exe";
        startInfo.Arguments = $"SlotScene {instanceId}"; // Passa o argumento para carregar a cena "SlotScene" e o ID da instância
        Process newProcess = Process.Start(startInfo);

        // Adiciona o processo à lista de instâncias abertas
        if (newProcess != null)
        {
            instanceProcesses.Add(newProcess);

            // Monitora o fechamento do processo
            newProcess.EnableRaisingEvents = true;
            newProcess.Exited += (sender, e) => OnInstanceProcessExited(newProcess, instanceId, newInstance);
        }

        // Envia uma mensagem para a nova instância
        SendMessageToServer($"{instanceId:0000} aberta.");

        // Inicia uma corrotina para esperar a confirmação do SlotController
        StartCoroutine(WaitForSlotControllerConfirmation(instanceId, newInstance));
    }

    // Método chamado quando o processo da instância é encerrado
    private void OnInstanceProcessExited(Process process, int instanceId, GameObject instanceUI)
    {
        // Remove a instância da lista de processos
        instanceProcesses.Remove(process);

        // Remove a instância da lista de IDs
        instanceIds.Remove(instanceId);

        // Remove a instância da interface do usuário
        if (instanceUI != null)
        {
            Destroy(instanceUI);
        }

        // Envia uma mensagem de log
        UnityEngine.Debug.Log($"Instância {instanceId:0000} foi fechada manualmente.");
        AddLogToUI(instanceId.ToString(), "Fechada manualmente.");
    }

    private IEnumerator WaitForSlotControllerConfirmation(int instanceId, GameObject newInstance)
    {
        float timeout = 10f; // Tempo máximo de espera em segundos
        float startTime = Time.time;

        bool confirmed = false;

        while (Time.time - startTime < timeout && !confirmed)
        {
            // Verifica se o SlotController confirmou que está aberto
            if (instanceIds.Contains(instanceId))
            {
                confirmed = true;
                break;
            }

            yield return null; // Espera até o próximo frame
        }

        if (!confirmed)
        {
            // Se o SlotController não confirmou, remove a instância
            RemoveInstance(newInstance, instanceId);
            UnityEngine.Debug.LogWarning($"Instância {instanceId:0000} não confirmou a abertura e foi removida.");
        }
    }

    // Método para remover uma instância
    private void RemoveInstance(GameObject instance, int instanceId)
    {
        // Remove a instância do Content
        Destroy(instance);

        // Remove o ID da lista de IDs
        instanceIds.Remove(instanceId);

        // Fecha a janela da instância correspondente
        Process processToClose = instanceProcesses.FirstOrDefault(p => p.Id == instanceId);
        if (processToClose != null && !processToClose.HasExited)
        {
            processToClose.CloseMainWindow(); // Fecha a janela da instância
            processToClose.Close(); // Libera os recursos do processo
            instanceProcesses.Remove(processToClose); // Remove o processo da lista
        }

        // Envia uma mensagem para a instância fechada
        SendMessageToServer($"Instância {instanceId:0000} fechada.");
    }

    void OnApplicationQuit()
    {
        // Para o servidor ao fechar a aplicação
        isServerRunning = false;
        if (server != null)
        {
            server.Stop();
        }

        // Fecha todas as instâncias abertas
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