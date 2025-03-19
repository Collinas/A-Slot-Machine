using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using System.Runtime.InteropServices;
using System;
using System.Threading;

/// <summary>
/// [Client] Controlador do jogo de slots, gerencia a lógica do jogo e a comunicação com o InstanceController.
/// </summary>
public class SlotController : MonoBehaviour
{
    [Header("Prefabs e Posições")]
    [Tooltip("Lista de prefabs para os números (1 a 9 + Wild)")]
    [SerializeField] private List<GameObject> prefabs;
    [Tooltip("Posição inicial do reel1")]
    [SerializeField] private Transform initialPosReel1;
    [Tooltip("Posição inicial do reel2")]
    [SerializeField] private Transform initialPosReel2;
    [Tooltip("Posição inicial do reel3")]
    [SerializeField] private Transform initialPosReel3;
    [Tooltip("Posições do reel1")]
    [SerializeField] private List<Transform> firstReel;
    [Tooltip("Posições do reel2")]
    [SerializeField] private List<Transform> secondReel;
    [Tooltip("Posições do reel3")]
    [SerializeField] private List<Transform> thirdReel;
    [Tooltip("Posição final do reel1")]
    [SerializeField] private Transform finalPosReel1;
    [Tooltip("Posição final do reel2")]
    [SerializeField] private Transform finalPosReel2;
    [Tooltip("Posição final do reel3")]
    [SerializeField] private Transform finalPosReel3;

    [Header("UI Elements")]
    [Tooltip("Texto para exibir o numero de créditos")]
    [SerializeField] private Text counterText;
    [Tooltip("Container para os números instanciados")]
    [SerializeField] private GameObject numberContainer;
    [Tooltip("Indicador de resultado (Barra horizontal)")]
    [SerializeField] private RawImage resultIndicator;
    [Tooltip("Texto do resultado ao lado a direita")]
    [SerializeField] private Text resultText;
    [Tooltip("Texto para exibir o Jackpot")]
    [SerializeField] private Text log001CounterText;

    [Header("Configurações de Jogo")]
    [Tooltip("Créditos iniciais")]
    [SerializeField] private int initialCredits = 1;
    [Tooltip("Duração da animação dos números")]
    [SerializeField] private float prefabMoveDuration = 0.5f;
    [Tooltip("Atraso entre giros")]
    [SerializeField] private float delayBetweenSpins = 0.5f;
    [Tooltip("Atraso para jogar novamente")]
    [SerializeField] private float delayToPlayAgain = 1.0f;

    [Header("Colors")]
    [Tooltip("Cor padrão do indicador")]
    [SerializeField] private Color defaultColor = Color.white;
    [Tooltip("Cor de vitória")]
    [SerializeField] private Color winColor = Color.green;
    [Tooltip("Cor de derrota")]
    [SerializeField] private Color loseColor = Color.red;

    private TcpClient client; // Cliente TCP para comunicação com o InstanceController
    private NetworkStream stream; // Stream de rede
    private string instanceId; // ID da instância
    private readonly List<GameObject> instantiatedPrefabs = new(); // Lista de prefabs instanciados
    private int counter; // Contador de créditos
    private bool isSpinning = false; // Flag para controlar se o jogo está girando

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool SetWindowText(IntPtr hWnd, string lpString);

    ///////////////////////////////////////////// InitializeSlots.cs

    /// <summary>
    /// Inicializa o controlador do slot, configurando a janela, créditos e conectando ao InstanceController.
    /// </summary>
    private void Start()
    {
        string[] args = Environment.GetCommandLineArgs();
        instanceId = args.Length > 2 ? args[2] : "UnknownInstance";

        IntPtr windowHandle = Process.GetCurrentProcess().MainWindowHandle;
        if (windowHandle != IntPtr.Zero)
        {
            SetWindowText(windowHandle, $"{instanceId}");
        }

        counter = initialCredits;
        resultIndicator.color = defaultColor;
        resultText.text = "";
        ValidateSetup();
        InitializeReels();
        UpdateUI();

        ConnectToInstanceController();
        SendMessageToInstanceController($"SlotControllerAtivo:{instanceId}");
    }

    /// <summary>
    /// Valida a configuração inicial do jogo.
    /// </summary>
    private void ValidateSetup()
    {
        if (prefabs.Count != 10 || firstReel.Count != 3 || secondReel.Count != 3 || thirdReel.Count != 3)
        {
            UnityEngine.Debug.LogError("Erro na configuração: É necessário exatamente 10 prefabs e 3 posições para cada reel.");
        }
    }

    /// <summary>
    /// Inicializa os reels do jogo com números aleatórios.
    /// </summary>
    private void InitializeReels()
    {
        for (int j = 0; j < 3; j++)
        {
            int randomIndex1 = UnityEngine.Random.Range(0, prefabs.Count);
            GameObject newPrefab1 = Instantiate(prefabs[randomIndex1], firstReel[j].position, Quaternion.identity, numberContainer.transform);
            newPrefab1.name = $"number{randomIndex1 + 1}_reel1";
            instantiatedPrefabs.Add(newPrefab1);

            int randomIndex2 = UnityEngine.Random.Range(0, prefabs.Count);
            GameObject newPrefab2 = Instantiate(prefabs[randomIndex2], secondReel[j].position, Quaternion.identity, numberContainer.transform);
            newPrefab2.name = $"number{randomIndex2 + 1}_reel2";
            instantiatedPrefabs.Add(newPrefab2);

            int randomIndex3 = UnityEngine.Random.Range(0, prefabs.Count);
            GameObject newPrefab3 = Instantiate(prefabs[randomIndex3], thirdReel[j].position, Quaternion.identity, numberContainer.transform);
            newPrefab3.name = $"number{randomIndex3 + 1}_reel3";
            instantiatedPrefabs.Add(newPrefab3);
        }
    }

    /// <summary>
    /// Conecta ao InstanceController via TCP.
    /// </summary>
    private void ConnectToInstanceController()
    {
        try
        {
            client = new TcpClient("127.0.0.1", 12345);
            stream = client.GetStream();
            Thread receiveThread = new Thread(new ThreadStart(ReceiveMessages));
            receiveThread.Start();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Erro ao conectar ao InstanceController: " + e.Message);
        }
    }

    ///////////////////////////////////////////// SlotToInstanceCommunication.cs

    /// <summary>
    /// Recebe mensagens do InstanceController.
    /// </summary>
    private void ReceiveMessages()
    {
        byte[] buffer = new byte[1024];
        int bytesRead;

        while (client.Connected)
        {
            try
            {
                bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    HandleMessageFromInstanceController(message);
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Erro ao receber mensagem do InstanceController: " + e.Message);
                break;
            }
        }
    }

    /// <summary>
    /// Manipula as mensagens recebidas do InstanceController.
    /// </summary>
    /// <param name="message">Mensagem recebida.</param>
    private void HandleMessageFromInstanceController(string message)
    {
        if (message.StartsWith("Log:"))
        {
            string[] parts = message.Split(':');
            if (parts.Length >= 3)
            {
                string logMessage = parts[2];
                UnityEngine.Debug.Log($"[{parts[1]}] {logMessage}");
            }
        }
        else if (message.StartsWith("LogCounterUpdate:"))
        {
            // Atualiza o contador de Jackpot no UI
            string[] parts = message.Split(':');
            if (parts.Length >= 2 && float.TryParse(parts[1], out float counterValue))
            {
                UpdateLog001CounterText(counterValue);
            }
        }
        else if (message.StartsWith("LogCounterReset:"))
        {
            // Adiciona o valor do contador antes do reset aos créditos da instância
            string[] parts = message.Split(':');
            if (parts.Length >= 2 && float.TryParse(parts[1], out float resetValue))
            {
                counter += (int)resetValue; // Convert to int if necessary
                UpdateCounterText();

                // Envia um novo log informando o valor adicionado
                string resetLogMessage = $"LOG006 JACKPOT INSTANCE={instanceId} VALUE={resetValue:F2} CREDITS={counter}";
                SendLogToInstanceController(resetLogMessage);
            }
        }
    }

    /// <summary>
    /// Envia uma mensagem para o InstanceController.
    /// </summary>
    /// <param name="message">Mensagem a ser enviada.</param>
    private void SendMessageToInstanceController(string message)
    {
        if (client == null || !client.Connected)
        {
            UnityEngine.Debug.LogWarning("Conexão com o InstanceController não está ativa.");
            return;
        }

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            stream.Write(data, 0, data.Length);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Erro ao enviar mensagem para o InstanceController: " + e.Message);
        }
    }

    /// <summary>
    /// Envia uma mensagem de log para o InstanceController.
    /// </summary>
    /// <param name="logMessage">Mensagem de log.</param>
    private void SendLogToInstanceController(string logMessage)
    {
        string fullLogMessage = $"Log:{instanceId}:{logMessage}";
        SendMessageToInstanceController(fullLogMessage);
    }

    ///////////////////////////////////////////// SlotGameController.cs

    /// <summary>
    /// Manipula a entrada do usuário para girar os reels, adicionar créditos ou sacar.
    /// </summary>
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Z) && !isSpinning) HandleSpin();
        if (Input.GetKeyDown(KeyCode.X) && !isSpinning) AddCredits(10);
        if (Input.GetKeyDown(KeyCode.C) && !isSpinning && counter > 0) CashoutCredits();
    }

    /// <summary>
    /// Inicia o giro dos reels.
    /// </summary>
    private void HandleSpin()
    {
        if (isSpinning) return;
        if (counter <= 0) return;
        isSpinning = true;
        resultIndicator.color = defaultColor;
        resultText.text = "";
        StartCoroutine(SpinReels());
        counter--;
        UpdateUI();

        string logMessage = "LOG001 START BET=1 CREDITS=" + counter;
        SendLogToInstanceController(logMessage);
    }

    /// <summary>
    /// Gira os reels e verifica se há uma combinação vencedora.
    /// </summary>
    /// <returns>Coroutine.</returns>
    private IEnumerator SpinReels()
    {
        StartCoroutine(TemporarySpinAnimation(prefabMoveDuration));
        yield return StartCoroutine(MoveAndDestroyExistingPrefabs());

        instantiatedPrefabs.Clear();

        int[] middleRowIndices = new int[3];

        for (int j = 0; j < 3; j++)
        {
            int randomIndex1 = UnityEngine.Random.Range(0, prefabs.Count);
            GameObject newPrefab1 = Instantiate(prefabs[randomIndex1], initialPosReel1.position, Quaternion.identity, numberContainer.transform);
            newPrefab1.name = $"number{randomIndex1 + 1}_reel1";
            instantiatedPrefabs.Add(newPrefab1);
            StartCoroutine(MovePrefab(newPrefab1, firstReel[2 - j].position, finalPosReel1, prefabMoveDuration));

            int randomIndex2 = UnityEngine.Random.Range(0, prefabs.Count);
            GameObject newPrefab2 = Instantiate(prefabs[randomIndex2], initialPosReel2.position, Quaternion.identity, numberContainer.transform);
            newPrefab2.name = $"number{randomIndex2 + 1}_reel2";
            instantiatedPrefabs.Add(newPrefab2);
            StartCoroutine(MovePrefab(newPrefab2, secondReel[2 - j].position, finalPosReel2, prefabMoveDuration));

            int randomIndex3 = UnityEngine.Random.Range(0, prefabs.Count);
            GameObject newPrefab3 = Instantiate(prefabs[randomIndex3], initialPosReel3.position, Quaternion.identity, numberContainer.transform);
            newPrefab3.name = $"number{randomIndex3 + 1}_reel3";
            instantiatedPrefabs.Add(newPrefab3);
            StartCoroutine(MovePrefab(newPrefab3, thirdReel[2 - j].position, finalPosReel3, prefabMoveDuration));

            if (j == 1)
            {
                middleRowIndices[0] = randomIndex1 + 1;
                middleRowIndices[1] = randomIndex2 + 1;
                middleRowIndices[2] = randomIndex3 + 1;
            }

            yield return new WaitForSeconds(delayBetweenSpins);
        }

        yield return new WaitForSeconds(delayBetweenSpins);

        CheckWinningCombination(middleRowIndices);
        yield return new WaitForSeconds(delayToPlayAgain);
        isSpinning = false;
    }

    /// <summary>
    /// Executa uma animação temporária de giro dos reels.
    /// </summary>
    /// <param name="duration">Duração da animação.</param>
    /// <returns>Coroutine.</returns>
    private IEnumerator TemporarySpinAnimation(float duration)
    {
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            int randomIndex1 = UnityEngine.Random.Range(0, prefabs.Count);
            GameObject tempPrefab1 = Instantiate(prefabs[randomIndex1], initialPosReel1.position, Quaternion.identity, numberContainer.transform);
            StartCoroutine(MovePrefab(tempPrefab1, finalPosReel1.position, finalPosReel1, prefabMoveDuration));
            Destroy(tempPrefab1, prefabMoveDuration);

            int randomIndex2 = UnityEngine.Random.Range(0, prefabs.Count);
            GameObject tempPrefab2 = Instantiate(prefabs[randomIndex2], initialPosReel2.position, Quaternion.identity, numberContainer.transform);
            StartCoroutine(MovePrefab(tempPrefab2, finalPosReel2.position, finalPosReel2, prefabMoveDuration));
            Destroy(tempPrefab2, prefabMoveDuration);

            int randomIndex3 = UnityEngine.Random.Range(0, prefabs.Count);
            GameObject tempPrefab3 = Instantiate(prefabs[randomIndex3], initialPosReel3.position, Quaternion.identity, numberContainer.transform);
            StartCoroutine(MovePrefab(tempPrefab3, finalPosReel3.position, finalPosReel3, prefabMoveDuration));
            Destroy(tempPrefab3, prefabMoveDuration);

            elapsedTime += delayBetweenSpins;
            yield return new WaitForSeconds(delayBetweenSpins);
        }
    }

    /// <summary>
    /// Move um prefab para uma posição final.
    /// </summary>
    /// <param name="prefab">Prefab a ser movido.</param>
    /// <param name="finalDestination">Posição final.</param>
    /// <param name="moveToObject">Objeto de destino.</param>
    /// <param name="duration">Duração do movimento.</param>
    /// <returns>Coroutine.</returns>
    private IEnumerator MovePrefab(GameObject prefab, Vector3 finalDestination, Transform moveToObject, float duration)
    {
        if (prefab == null) yield break;

        Vector3 startPosition = prefab.transform.position;
        Vector3 moveToPosition = moveToObject.position;
        float distance = Vector3.Distance(startPosition, moveToPosition);
        float speed = distance / duration;
        float elapsedTime = 0;

        while (elapsedTime < duration)
        {
            if (prefab == null) yield break;

            prefab.transform.position = Vector3.MoveTowards(prefab.transform.position, finalDestination, speed * Time.deltaTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (prefab != null)
        {
            prefab.transform.position = finalDestination;
        }
    }

    /// <summary>
    /// Move e destrói os prefabs existentes.
    /// </summary>
    /// <returns>Coroutine.</returns>
    private IEnumerator MoveAndDestroyExistingPrefabs()
    {
        var moveCoroutines = new List<Coroutine>();

        foreach (var prefab in instantiatedPrefabs)
        {
            if (prefab != null)
            {
                if (prefab.name.Contains("_reel1"))
                {
                    moveCoroutines.Add(StartCoroutine(MovePrefab(prefab, finalPosReel1.position, finalPosReel1, prefabMoveDuration)));
                }
                else if (prefab.name.Contains("_reel2"))
                {
                    moveCoroutines.Add(StartCoroutine(MovePrefab(prefab, finalPosReel2.position, finalPosReel2, prefabMoveDuration)));
                }
                else if (prefab.name.Contains("_reel3"))
                {
                    moveCoroutines.Add(StartCoroutine(MovePrefab(prefab, finalPosReel3.position, finalPosReel3, prefabMoveDuration)));
                }
            }
        }

        foreach (var coroutine in moveCoroutines)
        {
            yield return coroutine;
        }

        foreach (var prefab in instantiatedPrefabs)
        {
            if (prefab != null)
            {
                Destroy(prefab);
            }
        }
    }

    /// <summary>
    /// Verifica se há uma combinação vencedora nos reels.
    /// </summary>
    /// <param name="middleRow">Índices dos números na linha do meio.</param>
    private void CheckWinningCombination(int[] middleRow)
    {
        bool hasWinningCombo = middleRow[0] == middleRow[1] && middleRow[1] == middleRow[2];
        bool hasJoker = middleRow[0] == 10 || middleRow[1] == 10 || middleRow[2] == 10;
        bool isPartialMatch = hasJoker && (middleRow[0] == middleRow[1] || middleRow[1] == middleRow[2] || middleRow[0] == middleRow[2]);
        bool isWinningCombination = false;
        int creditsGained = 0;

        if (hasWinningCombo || isPartialMatch)
        {
            isWinningCombination = true;
            int extraCredits = middleRow[0] + middleRow[1] + middleRow[2];
            creditsGained = extraCredits;
            counter += extraCredits;
            string logMessage = "LOG004 WIN GAIN=" + extraCredits + " CREDITS=" + counter;
            SendLogToInstanceController(logMessage);
            UpdateCounterText();
        }

        if (isWinningCombination)
        {
            resultIndicator.color = winColor;
            resultText.text = $"+{creditsGained}";
        }
        else
        {
            resultIndicator.color = loseColor;
            resultText.text = "+0";
            string logMessage = "LOG005 LOSE CREDITS=" + counter;
            SendLogToInstanceController(logMessage);
        }
    }

    ///////////////////////////////////////////// CreditsManagement.cs

    /// <summary>
    /// Adiciona créditos ao contador.
    /// </summary>
    /// <param name="amount">Quantidade de créditos a adicionar.</param>
    private void AddCredits(int amount)
    {
        counter += amount;
        UpdateCounterText();
        string logMessage = "LOG002 ADD AMOUNT=" + amount + " CREDITS=" + counter;
        SendLogToInstanceController(logMessage);
    }

    /// <summary>
    /// Saca todos os créditos disponíveis.
    /// </summary>
    private void CashoutCredits()
    {
        string logMessage = "LOG003 CASH AMOUNT=" + counter;
        SendLogToInstanceController(logMessage);
        counter = 0;
        UpdateCounterText();
    }

    ///////////////////////////////////////////// SlotsUI.cs

    /// <summary>
    /// Atualiza a interface do usuário.
    /// </summary>
    private void UpdateUI()
    {
        UpdateCounterText();
    }

    /// <summary>
    /// Atualiza o texto do contador de créditos.
    /// </summary>
    private void UpdateCounterText()
    {
        counterText.text = counter.ToString();
    }

    /// <summary>
    /// Atualiza o texto do contador de Jackpot na interface do usuário.
    /// </summary>
    /// <param name="counterValue">Valor do contador.</param>
    private void UpdateLog001CounterText(float counterValue)
    {
        if (log001CounterText != null)
        {
            log001CounterText.text = $"{counterValue:F2}"; // Format to 2 decimal places
        }
    }

    ///////////////////////////////////////////// SlotQuit.cs

    /// <summary>
    /// Método chamado quando a aplicação é encerrada. Envia uma mensagem ao InstanceController.
    /// </summary>
    private void OnApplicationQuit()
    {
        SendMessageToInstanceController($"SlotControllerEncerrado:{instanceId}");
        client?.Close();
    }
}