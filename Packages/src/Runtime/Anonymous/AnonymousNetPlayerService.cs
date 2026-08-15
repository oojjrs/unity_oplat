using System.Threading.Tasks;

namespace oojjrs.oplat.anonymous
{
    internal class AnonymousNetPlayerService : MyNetPlayerServiceInterface
    {
        Task MyNetPlayerServiceInterface.UpdateAsync(MyNetPlayerServiceInterface.UpdateConfigInterface config, MyNetPlayerServiceInterface.UpdateResultInterface result)
        {
            throw new System.NotImplementedException();
        }
    }
}
