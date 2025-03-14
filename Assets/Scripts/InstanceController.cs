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

public class InstanceController : MonoBehaviour
{
    [SerializeField]
    private List<Text> instances;
    private int currentIndex = 0;

    // Variáveis para comunicação via socket
    private TcpClient client;
    private TcpListener server;
    private Thread serverThread;
    private bool isServerRunning = false;

    // Tamanho inicial das janelas
    private int initialWidth = 800;  // Largura inicial
    private int initialHeight = 600; // Altura inicial

    // Lista para rastrear os processos das instâncias abertas
    private List<Process> instanceProcesses = new List<Process>();

    void Start()
    {
        // Define o tamanho da janela principal como 800x600
        SetWindowSize(initialWidth, initialHeight);

        // Verifica se a lista foi configurada no inspetor
        if (instances == null || instances.Count == 0)
        {
            UnityEngine.Debug.LogWarning("A lista de instâncias não foi configurada no inspetor.");
        }
        else
        {
            // Garante que todas as instâncias comecem fechadas
            for (int i = 0; i < instances.Count; i++)
            {
                instances[i].text = "Instancia " + (i + 1) + " - Fechada";
            }
        }

        // Verifica se a instância foi aberta com o argumento para carregar a cena "SlotScene"
        string[] args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && args[1] == "SlotScene")
        {
            SceneManager.LoadScene("SlotScene");

            // Redimensiona a janela para o tamanho inicial
            SetWindowSize(initialWidth, initialHeight);
        }

        // Inicia o servidor de comunicação se for a primeira instância
        if (currentIndex == 0)
        {
            StartServer();
        }
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
            // Aqui você pode processar a mensagem recebida e tomar ações apropriadas
        }

        client.Close();
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

    // Método para definir o tamanho da janela
    private void SetWindowSize(int width, int height)
    {
        Screen.SetResolution(width, height, false); // Define a resolução da tela
        UnityEngine.Debug.Log($"Tamanho da janela definido para {width}x{height}");
    }

    public void AddNextInstance()
    {
        if (currentIndex < instances.Count)
        {
            instances[currentIndex].text = "Instancia " + (currentIndex + 1) + " - Aberta";
            currentIndex++;

            // Abre uma nova instância da build
            string instanceName = instances[currentIndex - 1].text;
            UnityEngine.Debug.Log("Abrindo nova instância para: " + instanceName);

            // Inicia uma nova instância da build com o argumento para carregar a cena "SlotScene"
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = Application.dataPath + "/../" + Application.productName + ".exe";
            startInfo.Arguments = "SlotScene"; // Passa o argumento para carregar a cena "SlotScene"
            Process newProcess = Process.Start(startInfo);

            // Adiciona o processo à lista de instâncias abertas
            if (newProcess != null)
            {
                instanceProcesses.Add(newProcess);
            }

            // Envia uma mensagem para a nova instância
            SendMessageToServer("Instância " + currentIndex + " aberta.");
        }
        else
        {
            UnityEngine.Debug.Log("Todas as instâncias estão abertas");
        }
    }

    public void RemoveLastInstance()
    {
        if (currentIndex > 0)
        {
            currentIndex--;
            instances[currentIndex].text = "Instancia " + (currentIndex + 1) + " - Fechada";

            // Fecha a janela da última instância aberta
            if (instanceProcesses.Count > 0)
            {
                Process lastProcess = instanceProcesses.Last();
                if (!lastProcess.HasExited)
                {
                    lastProcess.CloseMainWindow(); // Fecha a janela da instância
                    lastProcess.Close(); // Libera os recursos do processo
                }
                instanceProcesses.Remove(lastProcess); // Remove o processo da lista
            }

            // Envia uma mensagem para a instância fechada
            SendMessageToServer("Instância " + (currentIndex + 1) + " fechada.");
        }
        else
        {
            UnityEngine.Debug.Log("Todas as instâncias estão fechadas");
        }
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