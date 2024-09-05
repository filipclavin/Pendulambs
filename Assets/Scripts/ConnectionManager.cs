using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class ConnectionManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _hostStatusText = null;
    [SerializeField] private TextMeshProUGUI _joinCodeText = null;

    [SerializeField] private TextMeshProUGUI _joinStatusText = null;
    [SerializeField] private TMP_InputField _joinCodeInput = null;

    public async void OnHostButtonClick()
    {
        string joinCode = await Host();
        if (joinCode == null)
        {
            _hostStatusText.text = "Failed to host";
            return;
        }

        _hostStatusText.text = "Host started";
        _joinCodeText.text = joinCode;
    }

    public async Task<string> Host()
    {
        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(2);
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(allocation, "dtls"));
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        return NetworkManager.Singleton.StartHost() ? joinCode : null;
    }

    public async void OnJoinButtonClick()
    {
        if (_joinCodeInput.text == "")
        {
            _joinStatusText.text = "Please enter a join code";
            return;
        }

        _joinStatusText.text = "Connecting...";

        bool success = await Join(_joinCodeInput.text);
        if (!success)
        {
            _joinStatusText.text = "Failed to join";
            return;
        }

        _joinStatusText.text = "Joining...";
    }

    public async Task<bool> Join(string joinCode)
    {
        if (string.IsNullOrEmpty(joinCode)) return false;

        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        var joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));
        return NetworkManager.Singleton.StartClient();
    }
}
