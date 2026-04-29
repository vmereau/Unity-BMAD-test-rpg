namespace Game.World
{
    public interface IInteractable
    {
        string InteractPrompt { get; }
        
        string NameTag { get; }
        void Interact();
    }
}
