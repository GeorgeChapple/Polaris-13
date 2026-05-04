using UnityEngine;

public class GravitySource : MonoBehaviour {

	public virtual Vector3 GetGravity (Vector3 position) {
		return Physics.gravity;
	}

    public virtual bool ProvidesOxygen(Vector3 position)
    {
        return false;
    }

    void OnEnable () {
		CustomGravity.Register(this);
	}

	void OnDisable () {
		CustomGravity.Unregister(this);
	}
}