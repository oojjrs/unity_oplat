namespace oojjrs.oplat.anonymous
{
    internal class AnonymousNetHostService : MyNetHostServiceInterface
    {
        private readonly AnonymousNet Net;
        private readonly HashQueue<MyNetRequest> Requests = new();
        private readonly HashQueue<MyNetResponse> Responses = new();

        internal AnonymousNetHostService(AnonymousNet net)
        {
            Net = net;
        }

        internal void HandleRequests()
        {
            if (Requests.Count > 0)
            {
                while (Requests.TryDequeue(out var request))
                    Net.HostResult.OnReceived(request);

                Net.HostResult.OnFinishThisHandling();
            }
        }

        internal bool HasResponses()
        {
            return Responses.Count > 0;
        }

        internal void Receive(MyNetRequest request)
        {
            Requests.Enqueue(request);
        }

        void MyNetHostServiceInterface.Send(MyNetResponse response)
        {
            Responses.Enqueue(response);
        }

        internal bool TryDequeue(out MyNetResponse response)
        {
            return Responses.TryDequeue(out response);
        }
    }
}
