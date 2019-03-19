﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Abstract behavior representing a behavior for an object that is currently being controlled by the user's inputs,
/// such as a player body or a ghost, which can be depicted facing one of the cardinal directions.
///
/// Provides the ability to change the direction being faced and synchronize that over the network. The logic
/// of how those changes are handled is left to the child classes.
/// </summary>
public abstract class UserControlledSprites : NetworkBehaviour
{
	[SyncVar(hook = nameof(FaceDirectionSync))]
	protected Orientation currentDirection;
	//cached other behaviors
	protected PlayerMove playerMove;
	protected RegisterPlayer registerPlayer;

	/// <summary>
	/// Current direction the sprite is facing
	/// </summary>
	public Orientation CurrentDirection
	{
		get => currentDirection;
	}


	/// <summary>
	/// true iff we are in the middle of a matrix rotation (between OnRotationStart and OnRotationEnd)
	/// </summary>
	protected bool isMatrixRotating;
	/// <summary>
	/// Destination orientation we will rotate to when OnRotationEnd happens
	/// </summary>
	private Orientation destinationOrientation;

    protected virtual void Awake()
    {
	    registerPlayer = GetComponent<RegisterPlayer>();

	    //Sub to matrix rotation events via the registerTile because it always has the
	    //correct matrix
	    registerPlayer.OnRotateStart.AddListener(OnRotationStart);
	    registerPlayer.OnRotateEnd.AddListener(OnRotationEnd);
    }

    protected virtual void OnDisable()
    {
	    registerPlayer.OnRotateStart.RemoveListener(OnRotationStart);
	    registerPlayer.OnRotateEnd.RemoveListener(OnRotationEnd);
    }

    public override void OnStartServer()
    {
	    LocalFaceDirection(Orientation.Down);
	    base.OnStartServer();
    }

    public override void OnStartClient()
    {
	    StartCoroutine(WaitForLoad());
	    base.OnStartClient();
    }

    /// <summary>
    /// Invoked from OnStartClient as a coroutine. Override this to modify the setup logic.
    /// <returns></returns>
    protected virtual IEnumerator WaitForLoad()
    {
	    yield return YieldHelper.EndOfFrame;
	    if (PlayerManager.LocalPlayer == gameObject)
	    {
		    LocalFaceDirection( currentDirection );
	    }
	    FaceDirectionSync(currentDirection);
    }

    private void OnRotationStart(RotationOffset fromCurrent, bool isInitialRotation)
    {
	    //ignore the initial rotation message because we determine initial rotation from the
	    //currentBodyDirection syncvar in playerSprites
	    if (!isInitialRotation)
	    {

		    destinationOrientation = currentDirection.Rotate(fromCurrent);
		    isMatrixRotating = true;
	    }
    }

    private void OnRotationEnd(RotationOffset fromCurrent, bool isInitialRotation)
    {

	    //ignore the initial rotation message because we determine initial rotation from the
	    //currentBodyDirection syncvar in playerSprites
	    if (!isInitialRotation)
	    {
		    LocalFaceDirection(destinationOrientation);
		    isMatrixRotating = false;
	    }
    }

    /// <summary>
    /// Cause player to face in the specified absolute orientation and syncs this change to the server / other
    /// hosts.
    /// </summary>
    /// <param name="newOrientation">new absolute orientation</param>
    public void ChangeAndSyncPlayerDirection(Orientation newOrientation)
    {
	    CmdChangeDirection(newOrientation);
	    //Prediction
	    LocalFaceDirection(newOrientation);
    }

    /// <summary>
    /// Change current facing direction to match direction (it's a Command so it's invoked on the server by
    /// the server itself or the client)
    /// </summary>
    /// <param name="direction">new direction</param>
    [Command]
    private void CmdChangeDirection(Orientation direction)
    {
	    LocalFaceDirection(direction);
    }

    /// <summary>
    /// Invoked when currentDirection syncvar changes.
    /// </summary>
    /// <param name="dir"></param>
    protected abstract void FaceDirectionSync(Orientation dir);

    /// <summary>
    /// Locally changes the direction of this player to face the specified direction but doesn't tell the server.
    /// If this is a client, only changes the direction locally and doesn't inform other players / server.
    /// If this is on the server, the direction change will be sent to all clients due to the syncvar.
    /// </summary>
    /// <param name="direction"></param>
    public abstract void LocalFaceDirection(Orientation direction);

    /// <summary>
    /// Overrides the local client prediction, forces the sprite to update based on the latest info we have from
    /// the server.
    /// </summary>
    public abstract void SyncWithServer();
}
