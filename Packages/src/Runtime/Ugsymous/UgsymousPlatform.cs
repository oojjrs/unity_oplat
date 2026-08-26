using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;

namespace oojjrs.oplat.ugsymous
{
    internal sealed class UgsymousPlatform : MonoBehaviour, MyPlatform.PlatformInterface
    {
        private UgsymousNet _net;
        private string _nickname;
        private Sprite _profileSprite;

        string MyPlatformServiceInterface.Account => AuthenticationService.Instance.PlayerId;
        bool MyPlatformServiceInterface.IsAlive => this != null;
        bool MyPlatformServiceInterface.IsRestartRequired => false;
        MyNetInterface MyPlatformServiceInterface.Net => _net;
        string MyPlatformServiceInterface.Nickname => _nickname;
        Sprite MyPlatformServiceInterface.ProfileSprite => _profileSprite;
        MyStorageServiceInterface MyPlatformServiceInterface.Storage { get; } = new UgsymousStorage();

        async Task MyPlatform.PlatformInterface.RunAsync(MyPlatformInitializer.CallbackInterface callback, CancellationToken cancellationToken)
        {
            await UnityServices.InitializeAsync();
            cancellationToken.ThrowIfCancellationRequested();

            if (AuthenticationService.Instance.IsSignedIn == false)
            {
                if (string.IsNullOrWhiteSpace(callback.AnonymousInstanceId) == false)
                    AuthenticationService.Instance.SwitchProfile(ToProfile(callback.AnonymousInstanceId));

                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            cancellationToken.ThrowIfCancellationRequested();
            _nickname = await AuthenticationService.Instance.GetPlayerNameAsync();
            _profileSprite = Resources.Load<Sprite>("AnonymousProfile");
            await VivoxService.Instance.InitializeAsync();
            if (VivoxService.Instance.IsLoggedIn == false)
                await VivoxService.Instance.LoginAsync();

            cancellationToken.ThrowIfCancellationRequested();
            _net = new UgsymousNet(AuthenticationService.Instance.PlayerId, callback);
        }

        private static string ToProfile(string value)
        {
            using var sha256 = SHA256.Create();
            return $"oplat-{ToHex(sha256.ComputeHash(Encoding.UTF8.GetBytes(value)))[..24]}";
        }

        internal static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var value in bytes)
                builder.Append(value.ToString("x2"));

            return builder.ToString();
        }

        private void Update() => _net?.Update();
        private void OnDestroy() => _net?.Dispose();
    }
}
