using System;
using UnityEditor;
using UnityEngine;

public class Car : MonoBehaviour {
    private const double _rotateTolerance = 0.1f;
    private float _forwardInput;
    private int _layerMask;

    private Rigidbody _rb;
    private float _rotateInput;
    public AxleInfo[] Axles;
    public Transform CenterOfMass;
    public float ForwardAcceleration = 800;
    public GameObject ForwardCOM;
    public float SuspensionForce = 1f;
    public float SuspensionLength;
    public float TurnStrength = 100;

    private void Start() {
        _rb = GetComponent<Rigidbody>();

        _layerMask = 1 << LayerMask.NameToLayer("Vehicle");
        _layerMask = ~_layerMask;
        _rb.centerOfMass = CenterOfMass.localPosition;
    }

    private void Update() {
        _forwardInput = Input.GetAxis("Vertical");
        _forwardInput = Mathf.Clamp(_forwardInput, -1, 1);

        _rotateInput = Input.GetAxis("Horizontal");
        _rotateInput = Mathf.Clamp(_rotateInput, -1, 1);
    }

    private void FixedUpdate() {
        // 0 = fully out, 1 = fully in
        foreach (var t in Axles) {
            t.LastLeftWheelResult = HandleWheel(t.LeftWheel, t.IsFront);
            t.LastRightWheelResult = HandleWheel(t.RightWheel, t.IsFront);
        }

        var groundForward = GetGroundBasedForward();
        var forwardDir = groundForward.IsSet ? groundForward.Heading : transform.forward;
        _rb.AddForceAtPosition(forwardDir * _forwardInput * ForwardAcceleration, ForwardCOM.transform.position);

        if (Math.Abs(_rotateInput) > _rotateTolerance) {
            _rb.AddRelativeTorque(Vector3.up * _rotateInput * TurnStrength);
        }
    }

    private FrontBackVectorPair GetGroundBasedForward() {
        var front = Vector3.zero;
        var back = Vector3.zero;

        foreach (var t in Axles) {
            if (t.LastLeftWheelResult == null || t.LastRightWheelResult == null) {
                continue;
            }

            var value = (t.LastLeftWheelResult.ImpactPoint + t.LastRightWheelResult.ImpactPoint) / 2f;
            if (t.IsFront) {
                front = value;
            } else if (t.IsBack) {
                back = value;
            }
        }

        return new FrontBackVectorPair {
            Front = front,
            Back = back
        };
    }

    private WheelRaycastResult HandleWheel(GameObject wheel, bool isFront) {
        if (!Physics.Raycast(wheel.transform.position, -transform.up, out var hit, SuspensionLength, _layerMask)) {
            return null;
        }

        var amount = Round(1f - hit.distance / SuspensionLength, 2);
        var result = new WheelRaycastResult {
            CompressionRatio = amount,
            ImpactPoint = hit.point
        };

        var pushBack = transform.up * SuspensionForce * amount;
        _rb.AddForceAtPosition(pushBack, wheel.transform.position);

        return result;
    }

    private static float Round(float value, int digits) {
        var multi = Mathf.Pow(10.0f, digits);
        return Mathf.Round(value * multi) / multi;
    }

    #region Gizmos

    private void OnDrawGizmos() {
        foreach (var t in Axles) {
            DebugGizmosWheelRaycastResult(t.LeftWheel, t.LastLeftWheelResult);
            DebugGizmosWheelRaycastResult(t.RightWheel, t.LastRightWheelResult);

            if (t.LastLeftWheelResult == null || t.LastRightWheelResult == null) {
                continue;
            }

            var halfWayVector = (t.LastLeftWheelResult.ImpactPoint + t.LastRightWheelResult.ImpactPoint) / 2f;
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(halfWayVector, .1f);
        }

        var frontBack = GetGroundBasedForward();
        Gizmos.DrawLine(frontBack.Front, frontBack.Back);
    }

    private void DebugGizmosWheelRaycastResult(GameObject wheel, WheelRaycastResult result) {
        var style = new GUIStyle { normal = { textColor = Color.white }, fontSize = 30 };

        var wheelPos = wheel.transform.position;

        if (result != null) {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(wheelPos, result.ImpactPoint);
            Gizmos.DrawSphere(result.ImpactPoint, 0.1f);

            Handles.Label(wheelPos, result.CompressionRatio.ToString(), style);
        } else {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(wheelPos, wheelPos - transform.up * SuspensionLength);

            Handles.Label(wheelPos, "No hit", style);
        }
    }

    #endregion
}

public class FrontBackVectorPair {
    public Vector3 Back;
    public Vector3 Front;

    public Vector3 Heading => Front - Back;
    public bool IsSet => Front != Vector3.zero && Back != Vector3.zero;
}

public class WheelRaycastResult {
    public float CompressionRatio;
    public Vector3 ImpactPoint;
}

[Serializable]
public class AxleInfo {
    public bool IsBack;

    public bool IsFront;
    public WheelRaycastResult LastLeftWheelResult;
    public WheelRaycastResult LastRightWheelResult;
    public GameObject LeftWheel;
    public GameObject RightWheel;
}