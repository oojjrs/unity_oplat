namespace oojjrs.oplat.anonymous
{
    internal class AnonymousNetMemberService : MyNetMemberServiceInterface
    {
        private readonly AnonymousNet Net;
        private readonly HashQueue<MyNetRequest> Requests = new();
        private readonly HashQueue<MyNetResponse> Responses = new();

        internal AnonymousNetMemberService(AnonymousNet net)
        {
            Net = net;
        }

        internal void HandleResponses()
        {
            if (Responses.Count > 0)
            {
                while (Responses.TryDequeue(out var response))
                    Net.MemberResult.OnReceived(response);

                Net.MemberResult.OnFinishThisHandling();
            }
        }

        internal bool HasRequest()
        {
            return Requests.Count > 0;
        }

        internal void Receive(MyNetResponse response)
        {
            Responses.Enqueue(response);
        }

        void MyNetMemberServiceInterface.Send(MyNetRequest request)
        {
            Requests.Enqueue(request);
        }

        internal bool TryDequeue(out MyNetRequest request)
        {
            return Requests.TryDequeue(out request);
        }
    }
}
