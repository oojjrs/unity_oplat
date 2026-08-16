namespace oojjrs.oplat
{
    public interface MyNetHostResultInterface
    {
        void OnFinishThisHandling();
        void OnReceived(MyNetRequest request);
    }
}
