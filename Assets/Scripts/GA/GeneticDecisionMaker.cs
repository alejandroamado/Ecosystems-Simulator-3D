using UnityEngine;
using EcosystemAI;
using System.Linq;

public class GeneticDecisionMaker : IDecisionMaker
{
    private float[] weights; // Genes, e.g., priorities for actions

    public GeneticDecisionMaker(float[] weights)
    {
        this.weights = weights;
    }

    public DecisionActionType ChooseAction(MonoBehaviour agent)
    {
        float total = weights.Sum();
        float roll = Random.Range(0f, total);
        float cumulative = 0f;

        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
            {
                return (DecisionActionType)i;
            }
        }

        return DecisionActionType.Rest; // Default fallback
    }

    public float[] GetWeights() => weights;
}
