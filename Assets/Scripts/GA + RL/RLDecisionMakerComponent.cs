using UnityEngine;

public class RLDecisionMakerComponent : MonoBehaviour
{
    public RLDecisionMaker DecisionMaker;

    private HerbivoreAI herb;

    void Awake()
    {
        DecisionMaker = new RLDecisionMaker();
        herb = GetComponent<HerbivoreAI>();
    }

}
