using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controlador de slots que gerencia a geração de números aleatórios, contagem de créditos e jackpot.
/// </summary>
public class SlotController : MonoBehaviour
{
    [Header("Prefabs e Posições")]
    [Tooltip("Lista de prefabs que serão instanciados nos reels.")]
    [SerializeField] private List<GameObject> prefabs;
    [Tooltip("Posição de spawn do reel1.")]
    [SerializeField] private Transform initialPosReel1;
    [Tooltip("Posição de spawn do reel2.")]
    [SerializeField] private Transform initialPosReel2;
    [Tooltip("Posição de spawn do reel3.")]
    [SerializeField] private Transform initialPosReel3;
    [Tooltip("Lista de 3 posições do reel1.")]
    [SerializeField] private List<Transform> firstReel;
    [Tooltip("Lista de 3 posições do reel2.")]
    [SerializeField] private List<Transform> secondReel;
    [Tooltip("Lista de 3 posições do reel3.")]
    [SerializeField] private List<Transform> thirdReel;
    [Tooltip("Posição para destruir prefabs do reel1.")]
    [SerializeField] private Transform finalPosReel1;
    [Tooltip("Posição para destruir prefabs do reel2.")]
    [SerializeField] private Transform finalPosReel2;
    [Tooltip("Posição para destruir prefabs do reel3.")]
    [SerializeField] private Transform finalPosReel3;

    [Header("UI Elements")]
    [Tooltip("Texto que exibe o contador de créditos.")]
    [SerializeField] private Text counterText;
    [Tooltip("Texto que exibe o valor do jackpot.")]
    [SerializeField] private Text jackpotText;
    [Tooltip("Container para os prefabs instanciados.")]
    [SerializeField] private GameObject numberContainer;
    [Tooltip("Barra horizontal indicador do resultado da rodada.")]
    [SerializeField] private RawImage resultIndicator;
    [Tooltip("Texto que exibe o créditos ganhos na rodada.")]
    [SerializeField] private Text resultText;

    [Header("Configurações de Jogo")]
    [Tooltip("Créditos iniciais do jogador.")]
    [SerializeField] private int initialCredits = 1;
    [Tooltip("Valor inicial do jackpot.")]
    [SerializeField] private float initialJackpot = 300f;
    [Tooltip("Incremento do jackpot a cada rodada de acordo com a aposta feita.")]
    [SerializeField] private float jackpotIncrement = 0.01f;
    [Tooltip("Duração do movimento dos prefabs.")]
    [SerializeField] private float prefabMoveDuration = 0.5f;
    [Tooltip("Atraso entre os spawn de prefabs nos reels.")]
    [SerializeField] private float delayBetweenSpins = 0.5f;
    [Tooltip("Atraso para jogar novamente após uma rodada.")]
    [SerializeField] private float delayToPlayAgain = 1.0f;

    [Header("Colors")]
    [Tooltip("Cor padrão do indicador de resultado.")]
    [SerializeField] private Color defaultColor = Color.white;
    [Tooltip("Cor do indicador de resultado em caso de ganho.")]
    [SerializeField] private Color winColor = Color.green;
    [Tooltip("Cor do indicador de resultado em caso de ganho nulo.")]
    [SerializeField] private Color loseColor = Color.red;

    // Lista de prefabs instanciados
    private readonly List<GameObject> instantiatedPrefabs = new();
    // Contador de créditos
    private int counter;
    // Valor do jackpot
    private float jackpot;
    // Indica se o jogo está acontecendo
    private bool isSpinning = false;

    private void Start()
    {
        //Define o counter e o jackpot de acordo com os valores iniciais
        counter = initialCredits;
        jackpot = initialJackpot;
        //Define os dois indicadores para default e nulo
        resultIndicator.color = defaultColor;
        resultText.text = "";
        //Verifica se a configuração inicial está de acordo com o PDF
        ValidateSetup();
        //Inicializa os reels com os prefabs em cada um dos 9 slots predefinidos
        InitializeReels();
        //Atualiza a interface do usuário atualizando o valor dos créditos e do jackpot
        UpdateUI();
    }

    /// <summary>
    /// Valida a configuração inicial do jogo.
    /// </summary>
    private void ValidateSetup()
    {
        if (prefabs.Count != 10 || firstReel.Count != 3 || secondReel.Count != 3 || thirdReel.Count != 3)
        {
            Debug.LogError("Erro na configuração: É necessário exatamente 10 prefabs e 3 posições para cada rolo.");
        }
    }

    /// <summary>
    /// Inicializa os 3 reels com 3 prefabs cada.
    /// </summary>
    private void InitializeReels()
    {
        for (int j = 0; j < 3; j++)
        {
            int randomIndex1 = Random.Range(0, prefabs.Count);
            GameObject newPrefab1 = Instantiate(prefabs[randomIndex1], firstReel[j].position, Quaternion.identity, numberContainer.transform);
            newPrefab1.name = $"number{randomIndex1 + 1}_reel1";
            instantiatedPrefabs.Add(newPrefab1);

            int randomIndex2 = Random.Range(0, prefabs.Count);
            GameObject newPrefab2 = Instantiate(prefabs[randomIndex2], secondReel[j].position, Quaternion.identity, numberContainer.transform);
            newPrefab2.name = $"number{randomIndex2 + 1}_reel2";
            instantiatedPrefabs.Add(newPrefab2);

            int randomIndex3 = Random.Range(0, prefabs.Count);
            GameObject newPrefab3 = Instantiate(prefabs[randomIndex3], thirdReel[j].position, Quaternion.identity, numberContainer.transform);
            newPrefab3.name = $"number{randomIndex3 + 1}_reel3";
            instantiatedPrefabs.Add(newPrefab3);
        }
    }

    /// <summary>
    /// Atualiza a interface do usuário com os valores atuais.
    /// </summary>
    private void UpdateUI()
    {
        UpdateCounterText();
        UpdateJackpotText();
    }

    private void Update()
    {
        ///<summary>
        ///Ao apertar Z o jogo irá rodar, 
        ///ao apertar X o jogador ganha 10 créditos e 
        ///ao apertar C o jogador irá retirar todos os créditos
        /// </summary>
        if (Input.GetKeyDown(KeyCode.Z) && !isSpinning) HandleSpin();
        if (Input.GetKeyDown(KeyCode.X) && !isSpinning) AddCredits(10);
        if (Input.GetKeyDown(KeyCode.C) && !isSpinning && counter > 0) CashoutCredits();
    }

    /// <summary>
    /// Lida com a rotação dos slots.
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
        jackpot += jackpotIncrement;
        UpdateUI();
        Debug.Log($"[Cassino Log] Jogada iniciada. Valor apostado: 1 crédito. Créditos restantes: {counter}");
    }

    /// <summary>
    /// Adiciona créditos ao contador.
    /// </summary>
    /// <param name="amount">Quantidade de créditos a adicionar.</param>
    private void AddCredits(int amount)
    {
        counter += amount;
        UpdateCounterText();
        Debug.Log($"[Cassino Log] Créditos inseridos: {amount}. Total de créditos: {counter}");
    }

    /// <summary>
    /// Realiza o cashout dos créditos.
    /// </summary>
    private void CashoutCredits()
    {
        Debug.Log($"[Cassino Log] Cashout realizado: {counter} créditos.");
        counter = 0;
        UpdateCounterText();
    }

    /// <summary>
    /// Gera números aleatórios e faz a animação dos rolos.
    /// </summary>
    private IEnumerator SpinReels()
    {
        StartCoroutine(TemporarySpinAnimation(prefabMoveDuration));
        yield return StartCoroutine(MoveAndDestroyExistingPrefabs());

        instantiatedPrefabs.Clear();

        int[] middleRowIndices = new int[3];

        for (int j = 0; j < 3; j++)
        {
            int randomIndex1 = Random.Range(0, prefabs.Count);
            GameObject newPrefab1 = Instantiate(prefabs[randomIndex1], initialPosReel1.position, Quaternion.identity, numberContainer.transform);
            newPrefab1.name = $"number{randomIndex1 + 1}_reel1";
            instantiatedPrefabs.Add(newPrefab1);
            StartCoroutine(MovePrefab(newPrefab1, firstReel[2 - j].position, finalPosReel1, prefabMoveDuration));

            int randomIndex2 = Random.Range(0, prefabs.Count);
            GameObject newPrefab2 = Instantiate(prefabs[randomIndex2], initialPosReel2.position, Quaternion.identity, numberContainer.transform);
            newPrefab2.name = $"number{randomIndex2 + 1}_reel2";
            instantiatedPrefabs.Add(newPrefab2);
            StartCoroutine(MovePrefab(newPrefab2, secondReel[2 - j].position, finalPosReel2, prefabMoveDuration));

            int randomIndex3 = Random.Range(0, prefabs.Count);
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
        CheckJackpot();
        yield return new WaitForSeconds(delayToPlayAgain);
        isSpinning = false;
    }

    /// <summary>
    /// Animação temporária de rotação dos rolos.
    /// </summary>
    private IEnumerator TemporarySpinAnimation(float duration)
    {
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            int randomIndex1 = Random.Range(0, prefabs.Count);
            GameObject tempPrefab1 = Instantiate(prefabs[randomIndex1], initialPosReel1.position, Quaternion.identity, numberContainer.transform);
            StartCoroutine(MovePrefab(tempPrefab1, finalPosReel1.position, finalPosReel1, prefabMoveDuration));
            Destroy(tempPrefab1, prefabMoveDuration);

            int randomIndex2 = Random.Range(0, prefabs.Count);
            GameObject tempPrefab2 = Instantiate(prefabs[randomIndex2], initialPosReel2.position, Quaternion.identity, numberContainer.transform);
            StartCoroutine(MovePrefab(tempPrefab2, finalPosReel2.position, finalPosReel2, prefabMoveDuration));
            Destroy(tempPrefab2, prefabMoveDuration);

            int randomIndex3 = Random.Range(0, prefabs.Count);
            GameObject tempPrefab3 = Instantiate(prefabs[randomIndex3], initialPosReel3.position, Quaternion.identity, numberContainer.transform);
            StartCoroutine(MovePrefab(tempPrefab3, finalPosReel3.position, finalPosReel3, prefabMoveDuration));
            Destroy(tempPrefab3, prefabMoveDuration);

            elapsedTime += delayBetweenSpins;
            yield return new WaitForSeconds(delayBetweenSpins);
        }
    }

    /// <summary>
    /// Move o prefab de uma posição inicial para uma posição final com velocidade constante.
    /// </summary>
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

    /// <summary>
    /// Verifica se a fileira do meio contém uma combinação vencedora.
    /// </summary>
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
            Debug.Log($"[Cassino Log] Jogada finalizada. Créditos ganhos: {extraCredits}. Total de créditos: {counter}");
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
            Debug.Log($"[Cassino Log] Jogada finalizada. Nenhum crédito ganho. Total de créditos: {counter}");
        }
    }

    /// <summary>
    /// Verifica se o jogador ganhou o jackpot.
    /// </summary>
    private void CheckJackpot()
    {
        bool isJackpot = false;
        int jackpotAmount = 0;

        if (Random.Range(1, 201) == 1)
        {
            isJackpot = true;
            int jackpotCredits = Mathf.FloorToInt(jackpot);
            jackpotAmount = jackpotCredits;
            counter += jackpotCredits;
            jackpot = initialJackpot;
            Debug.Log($"[Cassino Log] Jackpot ganho! Créditos ganhos: {jackpotCredits}. Total de créditos: {counter}");
            UpdateUI();
        }

        if (isJackpot)
        {
            resultIndicator.color = winColor;
            resultText.text = $"Jackpot +{jackpotAmount}";
        }
    }

    /// <summary>
    /// Atualiza o texto do contador de créditos.
    /// </summary>
    private void UpdateCounterText()
    {
        counterText.text = counter.ToString();
    }

    /// <summary>
    /// Atualiza o texto do jackpot.
    /// </summary>
    private void UpdateJackpotText()
    {
        jackpotText.text = Mathf.Floor(jackpot).ToString();
    }
}
