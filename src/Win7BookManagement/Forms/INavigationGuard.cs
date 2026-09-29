using System.Windows.Forms;

namespace Win7BookManagement.Forms
{
    public interface INavigationGuard
    {
        bool CanNavigateAway(IWin32Window owner);
    }
}
