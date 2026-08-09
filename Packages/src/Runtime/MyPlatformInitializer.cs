using System;
using UnityEngine;

namespace oojjrs.oplat
{
    [DisallowMultipleComponent]
    public class MyPlatformInitializer : MonoBehaviour
    {
        public interface CallbackInterface
        {
            MyPlatformTypeEnum InitialType { get; }

            void OnOk();
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
            try
            {
                await MyPlatform.CreatePlatform(_callback.InitialType).RunAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (callbackObject == null)
                return;

            _callback.OnOk();
        }
    }
}
