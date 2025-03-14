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
    [SerializeField] private List<GameObject> prefabs; // Lista de 10 prefabs
    [SerializeField] private Transform initialPosReel1; // Posição inicial para o primeiro rolo
    [SerializeField] private Transform initialPosReel2; // Posição inicial para o segundo rolo
    [SerializeField] private Transform initialPosReel3; // Posição inicial para o terceiro rolo
    [SerializeField] private List<Transform> firstReel; // Lista de 3 posições para o primeiro rolo
    [SerializeField] private List<Transform> secondReel; // Lista de 3 posições para o segundo rolo
    [SerializeField] private List<Transform> thirdReel; // Lista de 3 posições para o terceiro rolo
    [SerializeField] private Transform finalPosReel1; // Posição final para o primeiro rolo
    [SerializeField] private Transform finalPosReel2; // Posição final para o segundo rolo
    [SerializeField] private Transform finalPosReel3; // Posição final para o terceiro rolo

    [Header("UI Elements")]
    [SerializeField] private Text counterText; // Texto do contador de créditos
    [SerializeField] private Text jackpotText; // Texto do valor do jackpot
    [SerializeField] private GameObject numberContainer; // Container para os números instanciados
    [SerializeField] private RawImage resultIndicator;
    [SerializeField] private Text resultText; // Added field for result text

    [Header("Configurações de Jogo")]
    [SerializeField] private int initialCredits = 1; // Créditos iniciais
    [SerializeField] private float initialJackpot = 300f; // Jackpot inicial
    [SerializeField] private float jackpotIncrement = 0.01f; // Incremento do jackpot por rodada
    [SerializeField] private float prefabMoveDuration = 0.5f; // Duração do movimento dos prefabs
    [SerializeField] private float delayBetweenSpins = 0.5f; // Atraso entre as rotações e spawn dos prefabs temporários
    [SerializeField] private float delayToPlayAgain = 1.0f; // Atraso para jogar novamente

    [Header("Colors")]
    [SerializeField] private Color defaultColor = Color.white; // Color for default state
    [SerializeField] private Color winColor = Color.green; // Color for winning combination
    [SerializeField] private Color loseColor = Color.red; // Color for losing combination

    private List<GameObject> instantiatedPrefabs = new();
    private int counter;
    private float jackpot;
    private bool isSpinning = false;

    private void Start()
    {
        counter = initialCredits;
        jackpot = initialJackpot;
        resultIndicator.color = defaultColor;
        resultText.text = "";
        ValidateSetup();
        InitializeReels(); // Inicializa os rolos com 3 prefabs cada
        UpdateUI();
    }

    /// <summary>
    /// Inicializa os rolos com 3 prefabs cada.
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

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Z) && !isSpinning) HandleSpin();
        if (Input.GetKeyDown(KeyCode.X)) AddCredits(10);
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
    }

    /// <summary>
    /// Adiciona créditos ao contador.
    /// </summary>
    /// <param name="amount">Quantidade de créditos a adicionar.</param>
    private void AddCredits(int amount)
    {
        counter += amount;
        UpdateCounterText();
    }

    /// <summary>
    /// Gera números aleatórios e faz a animação dos rolos.
    /// </summary>
    private IEnumerator SpinReels()
    {
        // Start the temporary spin animation
        StartCoroutine(TemporarySpinAnimation(prefabMoveDuration));

        // Move existing prefabs to the final position and destroy them
        yield return StartCoroutine(MoveAndDestroyExistingPrefabs());

        // Clear the list of instantiated prefabs
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
        yield return new WaitForSeconds(delayToPlayAgain); // Adiciona o atraso para jogar novamente
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
        if (prefab == null) yield break; // Verifica se o prefab é nulo antes de iniciar a animação

        Vector3 startPosition = prefab.transform.position;
        Vector3 moveToPosition = moveToObject.position;
        float distance = Vector3.Distance(startPosition, moveToPosition);
        float speed = distance / duration;
        float elapsedTime = 0;

        while (elapsedTime < duration)
        {
            if (prefab == null) yield break; // Verifica se o prefab foi destruído durante a animação

            prefab.transform.position = Vector3.MoveTowards(prefab.transform.position, finalDestination, speed * Time.deltaTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (prefab != null) // Verifica se o prefab ainda existe antes de definir a posição final
        {
            prefab.transform.position = finalDestination;
        }
    }
    private IEnumerator MoveAndDestroyExistingPrefabs()
    {
        List<Coroutine> moveCoroutines = new List<Coroutine>();

        // Move all existing prefabs to their final positions
        foreach (var prefab in instantiatedPrefabs)
        {
            if (prefab != null)
            {
                // Determine which reel the prefab belongs to based on its name
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

        // Wait for all move coroutines to finish
        foreach (var coroutine in moveCoroutines)
        {
            yield return coroutine;
        }

        // Destroy all existing prefabs
        foreach (var prefab in instantiatedPrefabs)
        {
            if (prefab != null)
            {
                Destroy(prefab);
            }
        }
    }

    /// <summary>
    /// Remove os prefabs antigos antes de gerar novos.
    /// </summary>
    private void ClearPreviousPrefabs()
    {
        foreach (var prefab in instantiatedPrefabs)
        {
            Destroy(prefab);
        }
        instantiatedPrefabs.Clear();
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
            Debug.Log($"Ganhou {extraCredits} créditos!");
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
            Debug.Log($"Jackpot! Créditos ganhos: {jackpotCredits}");
            UpdateUI();
        }

        if (isJackpot)
        {
            resultIndicator.color = winColor;
            resultText.text = $"Jackpot +{jackpotAmount}";
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
