using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controlador de slots que gerencia a geração de números aleatórios, contagem de créditos e jackpot.
/// </summary>
public class SlotController : MonoBehaviour
{
    [SerializeField] private List<GameObject> prefabs; // Lista de 10 prefabs
    [SerializeField] private List<Transform> gridPositions; // Lista de 9 posições no grid
    [SerializeField] private Text counterText; // Texto do contador de créditos
    [SerializeField] private Text jackpotText; // Texto do valor do jackpot
    [SerializeField] private GameObject numberContainer; // Container para os números instanciados

    private List<GameObject> instantiatedPrefabs = new();
    private int counter = 1;
    private float jackpot = 300f;
    private readonly string[] positionNames = { "NO", "O", "SO", "N", "C", "S", "NE", "E", "SE" };

    private void Start()
    {
        ValidateSetup();
        UpdateUI();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Z)) HandleSpin();
        if (Input.GetKeyDown(KeyCode.X)) AddCredits(10);
    }

    /// <summary>
    /// Valida a configuração inicial do jogo.
    /// </summary>
    private void ValidateSetup()
    {
        if (prefabs.Count != 10 || gridPositions.Count != 9)
        {
            Debug.LogError("Erro na configuração: É necessário exatamente 10 prefabs e 9 posições no grid.");
        }
    }

    /// <summary>
    /// Lida com a rotação dos slots.
    /// </summary>
    private void HandleSpin()
    {
        if (counter <= 0) return;
        GenerateRandomPrefabs();
        counter--;
        jackpot += 0.01f;
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
    /// Gera números aleatórios no grid e verifica prêmios.
    /// </summary>
    private void GenerateRandomPrefabs()
    {
        ClearPreviousPrefabs();
        int[] middleRowIndices = new int[3];

        for (int i = 0; i < gridPositions.Count; i++)
        {
            int randomIndex = Random.Range(0, prefabs.Count);
            GameObject newPrefab = Instantiate(prefabs[randomIndex], gridPositions[i].position, Quaternion.identity, numberContainer.transform);
            newPrefab.name = $"number{randomIndex + 1}_{positionNames[i]}";
            instantiatedPrefabs.Add(newPrefab);

            if (i == 1 || i == 4 || i == 7)
            {
                middleRowIndices[(i == 1) ? 0 : (i == 4) ? 1 : 2] = randomIndex + 1;
            }
        }

        CheckWinningCombination(middleRowIndices);
        CheckJackpot();
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

        if (hasWinningCombo || isPartialMatch)
        {
            int extraCredits = middleRow[0] + middleRow[1] + middleRow[2];
            counter += extraCredits;
            Debug.Log($"Ganhou {extraCredits} créditos!");
            UpdateCounterText();
        }
    }

    /// <summary>
    /// Verifica se o jogador ganhou o jackpot.
    /// </summary>
    private void CheckJackpot()
    {
        if (Random.Range(1, 201) == 1)
        {
            int jackpotCredits = Mathf.FloorToInt(jackpot);
            counter += jackpotCredits;
            jackpot = 300f;
            Debug.Log($"Jackpot! Créditos ganhos: {jackpotCredits}");
            UpdateUI();
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
