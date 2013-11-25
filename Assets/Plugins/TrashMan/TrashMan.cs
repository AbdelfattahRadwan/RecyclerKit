using UnityEngine;
using System.Collections;
using System.Collections.Generic;



public partial class TrashMan : MonoBehaviour
{
	/// <summary>
	/// access to the singleton
	/// </summary>
	public static TrashMan instance;

	/// <summary>
	/// stores the recycle bins and is used to populate the Dictionaries at startup
	/// </summary>
	public List<TrashManRecycleBin> recycleBinCollection;

	/// <summary>
	/// uses the GameObject instanceId as its key for fast look-ups
	/// </summary>
	private Dictionary<int,TrashManRecycleBin> _instanceIdToRecycleBin = new Dictionary<int,TrashManRecycleBin>();

	/// <summary>
	/// uses the pool name to find the GameObject instanceId
	/// </summary>
	private Dictionary<string,int> _poolNameToInstanceId = new Dictionary<string,int>();

	[HideInInspector]
	public new Transform transform;


	#region MonoBehaviour

	private void Awake()
	{
		if( instance != null )
		{
			Destroy( gameObject );
		}
		else
		{
			transform = gameObject.transform;
			instance = this;
			initializePrefabPools();
		}

		StartCoroutine( cullExcessObjects() );
	}

	
	private void OnLevelWasLoaded()
	{
		// TODO: perhaps make this configurable per pool then add DontDestroyOnLoad. Currently this does nothing.
		_instanceIdToRecycleBin.Clear();
	}


	private void OnApplicationQuit()
	{
		instance = null;
	}
	
	#endregion


	#region Private

	/// <summary>
	/// coroutine that runs every couple seconds and removes any objects created over the recycle bins limit
	/// </summary>
	/// <returns>The excess objects.</returns>
	private IEnumerator cullExcessObjects()
	{
		var waiter = new WaitForSeconds( 5f );

		while( true )
		{
			for( var i = 0; i < recycleBinCollection.Count; i++ )
				recycleBinCollection[i].cullExcessObjects();

			yield return waiter;
		}
	}


	/// <summary>
	/// populats the lookup dictionaries
	/// </summary>
	private void initializePrefabPools()
	{
		if( recycleBinCollection == null )
			return;

		foreach( var recycleBin in recycleBinCollection )
		{
			if( recycleBin == null || recycleBin.prefab == null )
				continue;

			recycleBin.initialize();
			_instanceIdToRecycleBin.Add( recycleBin.prefab.prefabInstanceId, recycleBin );
			_poolNameToInstanceId.Add( recycleBin.prefab.name, recycleBin.prefab.prefabInstanceId );
		}
	}


	/// <summary>
	/// internal method that actually does the work of grabbing the item from the bin and returning it
	/// </summary>
	/// <param name="gameObjectInstanceId">Game object instance identifier.</param>
	private static GameObject spawn( int gameObjectInstanceId, Vector3 position, Quaternion rotation )
	{
		if( instance._instanceIdToRecycleBin.ContainsKey( gameObjectInstanceId ) )
		{
			var newGo = instance._instanceIdToRecycleBin[gameObjectInstanceId].spawn();

			if( newGo != null )
			{
				var newTransform = newGo.transform;
				newTransform.parent = null;
				newTransform.position = position;
				newTransform.rotation = rotation;
			}

			return newGo;
		}

		return null;
	}


	/// <summary>
	/// internal coroutine for despawning after a delay
	/// </summary>
	/// <returns>The despawn after delay.</returns>
	/// <param name="go">Go.</param>
	/// <param name="delayInSeconds">Delay in seconds.</param>
	private IEnumerator internalDespawnAfterDelay( GameObject go, float delayInSeconds )
	{
		yield return new WaitForSeconds( delayInSeconds );
		despawn( go );
	}

	#endregion


	#region Public

	public static void manageRecycleBin( TrashManRecycleBin recycleBin )
	{
		// make sure we can safely add the bin!
		if( instance._poolNameToInstanceId.ContainsKey( recycleBin.prefab.name ) )
		{
			Debug.LogError( "Cannot manage the recycle bin because there is already a GameObject with the name (" + recycleBin.prefab.name + ") being managed" );
			return;
		}

		instance.recycleBinCollection.Add( recycleBin );
		recycleBin.initialize();
		instance._instanceIdToRecycleBin.Add( recycleBin.prefab.prefabInstanceId, recycleBin );
		instance._poolNameToInstanceId.Add( recycleBin.prefab.name, recycleBin.prefab.prefabInstanceId );
	}


	/// <summary>
	/// pulls an object out of the recycle bin
	/// </summary>
	/// <param name="go">Go.</param>
	public static GameObject spawn( TrashManRecyclableObject recycleable, Vector3 position = default( Vector3 ), Quaternion rotation = default( Quaternion ) )
	{
		if( instance._instanceIdToRecycleBin.ContainsKey( recycleable.prefabInstanceId ) )
		{
			return spawn( recycleable.prefabInstanceId, position, rotation );
		}
		else
		{
			Debug.LogError( "attempted to spawn go (" + recycleable.gameObject.name + ") but there is no recycle bin setup for it. Falling back to Instantiate" );
			var newGo = GameObject.Instantiate( recycleable.gameObject, position, rotation ) as GameObject;
			newGo.transform.parent = null;
			
			return newGo;
		}
	}


	/// <summary>
	/// pulls an object out of the recycle bin
	/// </summary>
	/// <param name="go">Go.</param>
	public static GameObject spawn( GameObject go, Vector3 position = default( Vector3 ), Quaternion rotation = default( Quaternion ) )
	{
		var recycleableObject = go.GetComponent<TrashManRecyclableObject>();
		if( recycleableObject && instance._instanceIdToRecycleBin.ContainsKey( recycleableObject.prefabInstanceId ) )
		{
			return spawn( go.GetInstanceID(), position, rotation );
		}
		else
		{
			Debug.LogError( "attempted to spawn go (" + go.name + ") but there is no recycle bin setup for it. Falling back to Instantiate" );
			var newGo = GameObject.Instantiate( go, position, rotation ) as GameObject;
			newGo.transform.parent = null;
			
			return newGo;
		}
	}

	
	/// <summary>
	/// pulls an object out of the recycle bin using the bin's name
	/// </summary>
	public static GameObject spawn( string recycleBinName, Vector3 position = default( Vector3 ), Quaternion rotation = default( Quaternion ) )
	{
		int instanceId = -1;
		if( instance._poolNameToInstanceId.TryGetValue( recycleBinName, out instanceId ) )
		{
			return spawn( instanceId, position, rotation );
		}
		else
		{
			Debug.LogError( "attempted to spawn a GameObject from recycle bin (" + recycleBinName + ") but there is no recycle bin setup for it" );
			return null;
		}
	}
	
	
	/// <summary>
	/// sticks the GameObject back into it's recycle bin. If the GameObject has no bin it is destroyed.
	/// </summary>
	/// <param name="go">Go.</param>
	public static void despawn( GameObject go )
	{
		if( go == null )
			return;
		
		var trashManObject = go.GetComponent<TrashManRecyclableObject>();
		if( !trashManObject || !instance._instanceIdToRecycleBin.ContainsKey( trashManObject.prefabInstanceId ) )
		{
			Destroy( go );
		}
		else
		{
			instance._instanceIdToRecycleBin[trashManObject.prefabInstanceId].despawn( go );
			go.transform.parent = instance.transform;
		}
	}


	/// <summary>
	/// sticks the GameObject back into it's recycle bin. If the GameObject has no bin it is destroyed.
	/// </summary>
	/// <param name="go">Go.</param>
	public static void despawn( TrashManRecyclableObject trashManObject )
	{
		if( !trashManObject || !instance._instanceIdToRecycleBin.ContainsKey( trashManObject.prefabInstanceId ) )
		{
			Destroy( trashManObject.gameObject );
		}
		else
		{
			instance._instanceIdToRecycleBin[trashManObject.prefabInstanceId].despawn( trashManObject.gameObject );
			trashManObject.gameObject.transform.parent = instance.transform;
		}
	}
	
	
	/// <summary>
	/// sticks the GameObject back into it's recycle bin after a delay. If the GameObject has no bin it is destroyed.
	/// </summary>
	/// <param name="go">Go.</param>
	public static void despawnAfterDelay( GameObject go, float delayInSeconds )
	{	
		if( go == null )
			return;
		
		instance.StartCoroutine( instance.internalDespawnAfterDelay( go, delayInSeconds ) );
	}

	#endregion

}
