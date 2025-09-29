using UnityEngine;
using EcosystemAI;

/// <summary>
/// Monitorea las recompensas de cada agente y llama a Learn
/// tras cada acción.
/// </summary>
public class RLTrainer : MonoBehaviour
{
    // Llamar desde cada AI justo después de ejecutar la acción y aplicar reward:
    public void ReportStep(MonoBehaviour agent, int prevState, DecisionActionType action, float reward)
    {
        RLDecisionMaker dm = agent.GetComponent<RLDecisionMakerComponent>().DecisionMaker;
        dm.Learn(agent, prevState, action, reward);
    }
}
