
namespace Dependinator.Utils.UI;

interface IUIService
{
    event Action OnUIStateChange;
    void TriggerUIStateChange();
}


[Singleton]
class UIService : IUIService
{
    public event Action OnUIStateChange = null!;

    public void TriggerUIStateChange()
    {
        OnUIStateChange?.Invoke();
    }

}