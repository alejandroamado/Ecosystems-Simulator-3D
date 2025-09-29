using UnityEngine;
using EcosystemAI;

/// <summary>
/// Interfaz genérica para la toma de decisiones: 
/// TAction es el enum de acciones (p. ej. HerbivoreActionType o CarnivoreActionType).
/// </summary>
public interface IDecisionMaker
{
    DecisionActionType ChooseAction(MonoBehaviour agent);
}
