using UnityEngine;

namespace Operator.Dialogue
{
    [CreateAssetMenu(fileName = "DialogueSet", menuName = "Operator/Dialogue Set")]
    public class DialogueSet : ScriptableObject
    {
        [Tooltip("Уникальный ID диалога. Если пусто — берётся имя ассета.")]
        [SerializeField] string dialogueId;

        [SerializeField] DialogueLine[] lines;

        public string DialogueId => string.IsNullOrEmpty(dialogueId) ? name : dialogueId;
        public DialogueLine[] Lines => lines;
    }
}
