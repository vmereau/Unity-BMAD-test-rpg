namespace Game.World
{
    public interface IInteractable
    {
        string InteractPrompt { get; }

        string NameTag { get; }
        bool CanInteract { get; }   // NEW — false suppresses the prompt & Interact() in InteractionSystem
        void Interact();
    }
}
