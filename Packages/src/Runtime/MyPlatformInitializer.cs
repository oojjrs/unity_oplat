using System;
using UnityEngine;

namespace oojjrs.oplat
{
    [DisallowMultipleComponent]
    public class MyPlatformInitializer : MonoBehaviour
    {
        public interface CallbackInterface
        {
            string AnonymousInstanceId => null;
            uint AppId { get; }
            MyNetChatResultInterface ChatResult => EmptyResult;
            MyNetHostResultInterface HostResult => EmptyResult;
            MyPlatformTypeEnum InitialType { get; }
            MyNetMemberResultInterface MemberResult => EmptyResult;
            MyNetPlayerServiceInterface.UpdateResultInterface PlayerResult => EmptyResult;
            MyNetRoomServiceInterface.UpdateResultInterface RoomResult => EmptyResult;

            void OnResult(MyPlatformServiceInterface service);
        }

        private sealed class EmptyNetResult : MyNetChatResultInterface, MyNetHostResultInterface, MyNetMemberResultInterface, MyNetPlayerServiceInterface.UpdateResultInterface, MyNetRoomServiceInterface.UpdateResultInterface
        {
            void MyNetInterface.CatchInterface.OnBusy()
            {
            }

            void MyNetInterface.CatchInterface.OnException(MyNetSessionException e)
            {
            }

            void MyNetInterface.CatchInterface.OnFailed(MyNetInterface.CatchInterface.FailureEnum e)
            {
            }

            void MyNetChatResultInterface.OnReceived(string message, string playerId, string roomId)
            {
            }

            void MyNetHostResultInterface.OnFinishThisHandling()
            {
            }

            void MyNetHostResultInterface.OnReceived(MyNetRequest request)
            {
            }

            void MyNetMemberResultInterface.OnFinishThisHandling()
            {
            }

            void MyNetMemberResultInterface.OnReceived(MyNetResponse response)
            {
            }

            void MyNetPlayerServiceInterface.UpdateResultInterface.OnOk(MyNetPlayerInterface player)
            {
            }

            void MyNetRoomServiceInterface.UpdateResultInterface.OnOk(MyNetRoomInterface room)
            {
            }
        }

        private static readonly EmptyNetResult EmptyResult = new();

        private CallbackInterface _callback;

        private void Awake()
        {
            _callback = GetComponent<CallbackInterface>();
        }

        private async void Start()
        {
            var callbackObject = _callback as UnityEngine.Object;
            if (callbackObject == null)
            {
                Debug.LogWarning($"{name}> DON'T HAVE CALLBACK FUNCTION.");
                return;
            }

            var cancellationToken = destroyCancellationToken;
            var platform = MyPlatform.CreatePlatform(_callback.InitialType);
            try
            {
                await platform.RunAsync(_callback, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                MyPlatform.DestroyPlatform(platform);
                return;
            }
            catch
            {
                MyPlatform.DestroyPlatform(platform);
                throw;
            }

            if (callbackObject == null)
            {
                MyPlatform.DestroyPlatform(platform);
                return;
            }

            _callback.OnResult(platform);
        }
    }
}
