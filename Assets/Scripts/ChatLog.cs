using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class ChatLog : NetworkBehaviour
{
    [SerializeField] private TMP_InputField _chatInput = null;
    [SerializeField] private GameObject _messagePrefab = null;
    [SerializeField] private Vector2 _messageOffset = Vector2.zero;
    [SerializeField] private InputActionAsset _inputActions = null;

    private List<GameObject> _messages = new();

    private RectTransform _rectTransform;

    private void Start()
    {
        _rectTransform = GetComponent<RectTransform>();

        _chatInput.onSelect.AddListener(delegate { _inputActions.Disable(); });
        _chatInput.onSubmit.AddListener(delegate { SendChatMessage(); });
        _chatInput.onDeselect.AddListener(delegate { _inputActions.Enable(); });
    }

    public void SendChatMessage()
    {
        if (string.IsNullOrWhiteSpace(_chatInput.GetComponent<TMP_InputField>().text))
        {
            _chatInput.GetComponent<TMP_InputField>().text = string.Empty;
            return;
        }

        SendChatMessageRpc(new FixedString64Bytes(_chatInput.GetComponent<TMP_InputField>().text));
        _chatInput.GetComponent<TMP_InputField>().text = string.Empty;
    }

    [Rpc(SendTo.Everyone)]
    private void SendChatMessageRpc(FixedString64Bytes message, RpcParams rpcParams = default)
    {
        GameObject messageObj = Instantiate(_messagePrefab);
        _messages.Insert(0, messageObj);

        messageObj.GetComponent<TMP_Text>().text = message.ToString();
        messageObj.GetComponent<TMP_Text>().color = rpcParams.Receive.SenderClientId == NetworkManager.Singleton.LocalClientId ? Color.green : Color.blue;
        messageObj.GetComponent<RectTransform>().SetParent(_rectTransform);

        UpdateChatLog();
    }

    private void UpdateChatLog()
    {
        for (int i = 0; i < _messages.Count; i++)
        {
            GameObject message = _messages[i];
            RectTransform messageRectTransform = message.GetComponent<RectTransform>();

            Vector3 position = new (_messageOffset.x, _messageOffset.y * (i+1) + messageRectTransform.rect.height * i, 0f);
            Debug.Log("position.y + messageRectTransform.rect.height > _rectTransform.rect.height: " +
                      (position.y + messageRectTransform.rect.height > _rectTransform.rect.height));
            if (position.y + messageRectTransform.rect.height > _rectTransform.rect.height)
            {
                _messages.Remove(message);
                Destroy(message);
                i--;
                continue;
            }

            messageRectTransform.anchoredPosition = position;

        }
    }
}
