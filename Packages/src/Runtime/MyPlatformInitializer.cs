using System;
using UnityEngine;

namespace oojjrs.oplat
{
    [DisallowMultipleComponent]
    public class MyPlatformInitializer : MonoBehaviour
    {
        public interface CallbackInterface
        {
            string AnonymousInstanceId { get; }
            uint AppId { get; }
            MyNetHostResultInterface HostResult { get; }
            MyPlatformTypeEnum InitialType { get; }
            MyNetMemberResultInterface MemberResult { get; }
            MyNetRoomServiceInterface.UpdateResultInterface RoomResult { get; }

            void OnResult(MyPlatformServiceInterface service);
        }

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
