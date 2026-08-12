#if STEAMWORKS_NET
using System;
using System.Threading;
using System.Threading.Tasks;
using Steamworks;
using UnityEngine;

namespace oojjrs.oplat.steam
{
    internal sealed class SteamPlatform : MonoBehaviour, MyPlatform.PlatformInterface
    {
        private bool _isInitialized;
        private Sprite _profileImage;
        private Texture2D _profileImageTexture;

        string MyPlatformServiceInterface.Account => SteamUser.GetSteamID().ToString();
        bool MyPlatformServiceInterface.IsAlive => (this != null) && _isInitialized;
        string MyPlatformServiceInterface.Nickname => SteamFriends.GetPersonaName();
        Sprite MyPlatformServiceInterface.ProfileImage => _profileImage;

        private void OnDestroy()
        {
            if (_profileImage != null)
                Destroy(_profileImage);

            if (_profileImageTexture != null)
                Destroy(_profileImageTexture);

            if (_isInitialized == false)
                return;

            SteamAPI.Shutdown();
        }

        private void Update()
        {
            if (_isInitialized)
                SteamAPI.RunCallbacks();
        }

        Task MyPlatform.PlatformInterface.RunAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_isInitialized)
                return Task.CompletedTask;

            var result = SteamAPI.InitEx(out var errorMessage);
            if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                throw new InvalidOperationException($"Steam initialization failed ({result}): {errorMessage}");

            _isInitialized = true;
            _profileImage = CreateProfileImage();
            return Task.CompletedTask;
        }

        private Sprite CreateProfileImage()
        {
            var imageHandle = SteamFriends.GetMediumFriendAvatar(SteamUser.GetSteamID());
            if (imageHandle <= 0)
                return null;

            if (SteamUtils.GetImageSize(imageHandle, out var width, out var height) == false)
                return null;

            var data = new byte[checked((int)(width * height * 4))];
            if (SteamUtils.GetImageRGBA(imageHandle, data, data.Length) == false)
                return null;

            _profileImageTexture = new Texture2D(checked((int)width), checked((int)height), TextureFormat.RGBA32, false);
            _profileImageTexture.LoadRawTextureData(data);
            _profileImageTexture.Apply(false, true);

            var rect = new Rect(0f, 0f, _profileImageTexture.width, _profileImageTexture.height);
            return Sprite.Create(_profileImageTexture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
    }
}
#endif
