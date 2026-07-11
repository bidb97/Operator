using UnityEngine;

namespace Operator.Dialogue
{
    [CreateAssetMenu(fileName = "DialogueCatalog", menuName = "Operator/Dialogue Catalog")]
    public class DialogueCatalog : ScriptableObject
    {
        [SerializeField] DialogueSet[] dialogues;

        public DialogueSet FindById(string dialogueId)
        {
            if (dialogues == null || string.IsNullOrEmpty(dialogueId))
                return null;

            for (var i = 0; i < dialogues.Length; i++)
            {
                var dialogue = dialogues[i];
                if (dialogue != null && dialogue.DialogueId == dialogueId)
                    return dialogue;
            }

            return null;
        }
    }
}
