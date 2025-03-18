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

public class SlotController : MonoBehaviour
{
    [Header("Prefabs e Posições")]
    [SerializeField] private List<GameObject> prefabs;
    [SerializeField] private Transform initialPosReel1;
    [SerializeField] private Transform initialPosReel2;
    [SerializeField] private Transform initialPosReel3;
    [SerializeField] private List<Transform> firstReel;
    [SerializeField] private List<Transform> secondReel;
    [SerializeField] private List<Transform> thirdReel;
    [SerializeField] private Transform finalPosReel1;
    [SerializeField] private Transform finalPosReel2;
    [SerializeField] private Transform finalPosReel3;

    [Header("UI Elements")]
    [SerializeField] private Text counterText;
    [SerializeField] private GameObject numberContainer;
    [SerializeField] private RawImage resultIndicator;
    [SerializeField] private Text resultText;
    [SerializeField] private Text log001CounterText; // Novo campo para exibir o contador de LOG001

    [Header("Configurações de Jogo")]
    [SerializeField] private int initialCredits = 1;
    [SerializeField] private float prefabMoveDuration = 0.5f;
    [SerializeField] private float delayBetweenSpins = 0.5f;
    [SerializeField] private float delayToPlayAgain = 1.0f;

    [Header("Colors")]
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color winColor = Color.green;
    [SerializeField] private Color loseColor = Color.red;

    private TcpClient client;
    private NetworkStream stream;
    private string instanceId;
    private readonly List<GameObject> instantiatedPrefabs = new();
    private int counter;
    private bool isSpinning = false;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool SetWindowText(IntPtr hWnd, string lpString);

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

    private void OnApplicationQuit()
    {
        SendMessageToInstanceController($"SlotControllerEncerrado:{instanceId}");
        client?.Close();
    }

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
            // Atualiza o contador de LOG001 no UI
            string[] parts = message.Split(':');
            if (parts.Length >= 2 && int.TryParse(parts[1], out int counterValue))
            {
                UpdateLog001CounterText(counterValue);
            }
        }
        else if (message.StartsWith("LogCounterReset:"))
        {
            // Adiciona o valor do contador antes do reset aos créditos da instância
            string[] parts = message.Split(':');
            if (parts.Length >= 2 && int.TryParse(parts[1], out int resetValue))
            {
                counter += resetValue;
                UpdateCounterText();

                // Envia um novo log informando o valor adicionado
                string resetLogMessage = $"LOG006 JACKPOT INSTANCE={instanceId} VALUE={resetValue} CREDITS={counter}";
                SendLogToInstanceController(resetLogMessage);
            }
        }
    }

    private void UpdateLog001CounterText(int counterValue)
    {
        if (log001CounterText != null)
        {
            log001CounterText.text = $"{counterValue}";
        }
    }

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

    private void ValidateSetup()
    {
        if (prefabs.Count != 10 || firstReel.Count != 3 || secondReel.Count != 3 || thirdReel.Count != 3)
        {
            UnityEngine.Debug.LogError("Erro na configuração: É necessário exatamente 10 prefabs e 3 posições para cada rolo.");
        }
    }

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

    private void UpdateUI()
    {
        UpdateCounterText();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Z) && !isSpinning) HandleSpin();
        if (Input.GetKeyDown(KeyCode.X) && !isSpinning) AddCredits(10);
        if (Input.GetKeyDown(KeyCode.C) && !isSpinning && counter > 0) CashoutCredits();
    }

    private void SendLogToInstanceController(string logMessage)
    {
        string fullLogMessage = $"Log:{instanceId}:{logMessage}";
        SendMessageToInstanceController(fullLogMessage);
    }

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

    private void AddCredits(int amount)
    {
        counter += amount;
        UpdateCounterText();
        string logMessage = "LOG002 ADD AMOUNT=" + amount + " CREDITS=" + counter;
        SendLogToInstanceController(logMessage);
    }

    private void CashoutCredits()
    {
        string logMessage = "LOG003 CASH AMOUNT=" + counter;
        SendLogToInstanceController(logMessage);
        counter = 0;
        UpdateCounterText();
    }

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

    private void UpdateCounterText()
    {
        counterText.text = counter.ToString();
    }
}