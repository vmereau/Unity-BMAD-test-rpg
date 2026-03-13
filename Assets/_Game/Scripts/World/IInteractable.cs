namespace Game.World
{
    public interface IInteractable
    {
        string InteractPrompt { get; }
        void Interact();
    }
}
