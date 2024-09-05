using System;
using Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

enum MovementMode {
    Grounded,
    Hanging
}

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private InputActionAsset _playerControls = null;

    [SerializeField] private Transform _otherPlayer = null;

    [SerializeField] private float _swingFactor = 1f;
    [SerializeField] private float _centrifugalForce = 1f;
    [SerializeField] private float _swingDistanceThreshold = 1f;

    [SerializeField] private float _movementSpeed = 1f;
    [SerializeField] private float _jumpForce = 1f;

    [SerializeField] private bool _host = false;

    [SerializeField] private float _stunDuration = 1f;

    private Rigidbody _rb;

    private InputActionMap _groundedActionMap;
    private InputAction _walkAction;
    private InputAction _jumpAction;
    private InputAction _anchorAction;

    private InputActionMap _hangingActionMap;
    private InputAction _swingAction;

    private MovementMode _movementMode = MovementMode.Grounded;

    private NetworkVariable<float> _swingValue = new(writePerm: NetworkVariableWritePermission.Owner);
    private NetworkVariable<float> _walkValue = new(writePerm: NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> _grounded = new(writePerm: NetworkVariableWritePermission.Server);

    private float _stunTimer = 0f;

    private bool _disconnected = false;

    // Start is called before the first frame update
    void Start()
    {
        _rb = GetComponent<Rigidbody>();

        if (IsServer)
        {
            GetComponent<NetworkObject>().ChangeOwnership(_host ? 0UL : 1UL);
            InitOwnerRpc();
        }

        NetworkManager.OnClientDisconnectCallback += clientId =>
        {
            if (clientId == OwnerClientId)
            {
                _disconnected = true;
            }
        };
    }

    [Rpc(SendTo.Owner)]
    void InitOwnerRpc()
    {
        _groundedActionMap = _playerControls.FindActionMap("Grounded");
        _groundedActionMap.Enable();
        _walkAction = _groundedActionMap.FindAction("Walk");
        _jumpAction = _groundedActionMap.FindAction("Jump");
        _anchorAction = _groundedActionMap.FindAction("Anchor");

        _hangingActionMap = _playerControls.FindActionMap("Hanging");
        _swingAction = _hangingActionMap.FindAction("Swing");

        _jumpAction.performed += _ =>
        {
            if (_grounded.Value) JumpRpc();
        };

        Camera.main.GetComponent<CinemachineBrain>().ActiveVirtualCamera.Follow = transform;
    }

    // Update is called once per frame
    void Update()
    {
        if (IsOwner && !_disconnected) OwnerUpdate();
        if (IsServer) ServerUpdate();
    }

    void OwnerUpdate()
    {
        if (Math.Abs(_swingAction.ReadValue<float>() - _swingValue.Value) > 0.1f)
        {
            _swingValue.Value = _swingAction.ReadValue<float>();
        }

        if (Math.Abs(_walkAction.ReadValue<float>() - _walkValue.Value) > 0.1f)
        {
            _walkValue.Value = _walkAction.ReadValue<float>();
        }

        if (_anchorAction.WasPressedThisFrame())
        {
            AnchorRpc();
        }

        if (_anchorAction.WasReleasedThisFrame())
        {
            UnAnchorRpc();
        }
    }

    void ServerUpdate()
    {
        if (_stunTimer > 0)
        {
            _stunTimer -= Time.deltaTime;
            if (_stunTimer <= 0)
            {
                UnStunRpc();
            }
        }

        if (_swingValue.Value != 0f) Swing(_swingValue.Value);
        if (_walkValue.Value != 0f) Walk(_walkValue.Value);

        if (_movementMode != MovementMode.Hanging &&_otherPlayer.position.y - transform.position.y > _swingDistanceThreshold)
        {

            if (!_grounded.Value) SetMovementModeRpc(MovementMode.Hanging);
        }
    }

    void Swing(float value)
    {
        float distance = Vector3.Distance(transform.position, _otherPlayer.position);
        if (distance < _swingDistanceThreshold) return;

        float signedSwingFactor = value * _swingFactor;
        Vector3 direction = (transform.position - _otherPlayer.position).normalized;
        Vector3 swingForce = signedSwingFactor * (Quaternion.AngleAxis(90, Vector3.forward) * direction);

        _rb.AddForce((swingForce + direction * _centrifugalForce) * Time.deltaTime);
    }

    void Walk(float direction)
    {
        Debug.Log($"Walking in direction {direction}");
        _rb.velocity = new Vector3(direction * _movementSpeed, _rb.velocity.y, _rb.velocity.z);
    }

    [Rpc(SendTo.Server)]
    void JumpRpc()
    {
        _rb.AddForce(Vector3.up * _jumpForce);
    }

    [Rpc(SendTo.Server)]
    void AnchorRpc()
    {
        SetRigidBodyConstraintsRpc(RigidbodyConstraints.FreezeAll);
    }

    [Rpc(SendTo.Server)]
    void UnAnchorRpc()
    {
        SetRigidBodyConstraintsRpc(RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        if (collision.gameObject.CompareTag("Ground") && collision.GetContact(0).normal.y > 0)
        {
            _grounded.Value = true;
            SetMovementModeRpc(MovementMode.Grounded);
        }

        if (collision.gameObject.CompareTag("Projectile"))
        {
            Destroy(collision.gameObject);
            StunRpc();
            _stunTimer = _stunDuration;
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if (!IsServer) return;

        if (collision.gameObject.CompareTag("Ground"))
        {
            _grounded.Value = false;
        }
    }

    [Rpc(SendTo.Everyone)]
    void SetRigidBodyConstraintsRpc(RigidbodyConstraints constraints)
    {
        if (!_rb) _rb = GetComponent<Rigidbody>();
        _rb.constraints = constraints;
    }

    [Rpc(SendTo.Everyone)]
    void SetMovementModeRpc(MovementMode mode)
    {
        _movementMode = mode;
        if (!_rb) _rb = GetComponent<Rigidbody>();

        switch (mode)
        {
            case MovementMode.Grounded:
                if (IsOwner)
                {
                    _groundedActionMap.Enable();
                    _hangingActionMap.Disable();
                }

                transform.rotation = Quaternion.identity;
                _rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
                break;

            case MovementMode.Hanging:
                if (IsOwner)
                {
                    _hangingActionMap.Enable();
                    _groundedActionMap.Disable();
                }

                _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezePositionZ;
                break;
        }
    }

    [Rpc(SendTo.Everyone)]
    void StunRpc()
    {
        GetComponent<Collider>().enabled = false;
        _rb.velocity = Vector3.zero;
        _playerControls.Disable();
    }

    [Rpc(SendTo.Everyone)]
    void UnStunRpc()
    {
        GetComponent<Collider>().enabled = true;
        _playerControls.Enable();
    }

}
