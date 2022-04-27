using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Known flaws of this script:
/// 
/// - Some rooms are not connected by corridors and therefore unreachable
/// - There is a small change the player will spawn in an unreachable room
/// - Corridors are generated using sampled sweeps along the horizontal and vertical axis
///   this may result in the having square-like corridor patterns
/// </summary>

public enum CellState
{
    Empty,
    Room,
    NextToRoom
}

public class Room
{
    public Room(int width, int length)
    {
        Width = width;
        Length = length;
    }

    /// <summary>
    /// The position of the room on the grid, this position always refers to the bottom left tile of the room
    /// </summary>
    public Vector2Int gridPosition;
    public int Width { get; }
    public int Length { get; }
}

public class DungeonGenerator : MonoBehaviour
{
    [SerializeField] private int _gridSizeX = 100;
    [SerializeField] private int _gridSizeY = 100;
    [SerializeField] private int _roomCount = 50;
    [SerializeField] private int _roomMinWidth = 5;
    [SerializeField] private int _roomMaxWidth = 10;
    [SerializeField] private int _roomMinHeight = 5;
    [SerializeField] private int _roomMaxHeight = 10;
    [SerializeField] private int _attemptsPerRoom = 50;
    [SerializeField] private int _corridorSample = 10;
    [SerializeField] private int _corridorWidth = 2;

    public GameObject playerPrefab;
    
    public Material mat;

    private CellState[,] _gridData;
    private List<Room> _rooms = new List<Room>();
    
    private void Start()
    {
        if (Application.isEditor)
        {
            UnityEditor.EditorWindow.FocusWindowIfItsOpen(typeof(UnityEditor.SceneView));
        }

        _gridData = new CellState[_gridSizeX, _gridSizeY];
        for (var x = 0; x < _gridSizeX; x++)
        {
            for (var y = 0; y < _gridSizeY; y++)
            {
                _gridData[x, y] = CellState.Empty;
            }
        }
        
        GenerateRooms(_roomCount);
        
        PlaceRooms();

        PlaceHallways();
        
        RenderDungeon();

        var firstRoom = _rooms[0];
        var playerpos = new Vector3(
            firstRoom.gridPosition.x + (firstRoom.Width * 0.5F),
            1,
            firstRoom.gridPosition.y + (firstRoom.Length * 0.5F)
        );

        Instantiate(playerPrefab, playerpos, Quaternion.identity);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Application.Quit();
    }

    private void PlaceHallways()
    {
        // Vertical corridor samples
        for (var x = _corridorSample - 1; x < _gridSizeX; x += _corridorSample)
        {
            var firstIndex = -1;
            var lastIndex = -1;
            
            for (int y = 0; y < _gridSizeY; y++)
            {
                if (firstIndex == -1 && _gridData[x, y] == CellState.Room) firstIndex = y;
                if (_gridData[x, y] == CellState.Room) lastIndex = y;
            }
            
            if (firstIndex == -1 || lastIndex == -1) continue;

            for (int w = 0; w < _corridorWidth; w++)
            {
                for (int cy = firstIndex; cy <= lastIndex; cy++)
                {
                    var xw = x + w;
                    if (xw >= _gridSizeX) continue;
                    
                    _gridData[xw, cy] = CellState.Room;
                }
            }
        }
        
        // Horizontal corridor samples
        for (var y = _corridorSample - 1; y < _gridSizeY; y += _corridorSample)
        {
            var firstIndex = -1;
            var lastIndex = -1;
            
            for (var x = 0; x < _gridSizeX; x++)
            {
                if (firstIndex == -1 && _gridData[x, y] == CellState.Room) firstIndex = x;
                if (_gridData[x, y] == CellState.Room) lastIndex = x;
            }
            if (firstIndex == -1 || lastIndex == -1) continue;

            for (int w = 0; w < _corridorWidth; w++)
            {
                for (var cx = firstIndex; cx <= lastIndex; cx++)
                {
                    var yw = y + w;
                    if (yw >= _gridSizeY) continue;
                    
                    _gridData[cx, yw] = CellState.Room;
                }
            }
        }
    }

    private void GenerateRooms(int roomCount)
    {
        for (var i = 0; i < roomCount; i++)
        {
            var room = new Room(Random.Range(_roomMinWidth, _roomMaxWidth), Random.Range(_roomMinHeight, _roomMaxHeight));
            _rooms.Add(room);
        }
    }
    
    private void PlaceRooms()
    {
        var newRooms = new List<Room>();
        
        foreach (var room in _rooms)
        {
            var isRoomPlaced = AttemptRoomPlacement(room);
            if (isRoomPlaced) newRooms.Add(room);
        }

        _rooms = newRooms;
    }
    
    private bool AttemptRoomPlacement(Room room)
    {
        for (var i = 0; i < _attemptsPerRoom; i++)
        {
            var randomPos = new Vector2Int(Random.Range(0, _gridSizeX), Random.Range(0, _gridSizeY));
            
            // if room doesn't fit on the grid skip this attempt and go to the next attempt
            if (randomPos.x + room.Width > _gridSizeX || randomPos.y + room.Length > _gridSizeY)
                continue;
            
            // if room overlaps with other rooms skip this attempt and go to the next attempt
            var isOverlapping = false;
            for (var x = randomPos.x - 1; x < randomPos.x + room.Width + 1; x++)
            {
                for (var y = randomPos.y - 1; y < randomPos.y + room.Length + 1; y++)
                {
                    // Check if x and y index are within array length
                    if (x < 0 || y < 0 || x >= _gridData.GetLength(0) || y >= _gridData.GetLength(1))
                        continue;
                        
                    if (_gridData[x, y] == CellState.Room)
                    {
                        isOverlapping = true;
                        break;
                    }
                }
                if (isOverlapping) break;
            }
            if (isOverlapping) continue;
            
            // At this point the room has room to be placed so i set the room position and update the grid
            room.gridPosition = randomPos;
            for (var x = randomPos.x - 1; x < randomPos.x + room.Width + 1; x++)
            {
                for (var y = randomPos.y - 1; y < randomPos.y + room.Length + 1; y++)
                {
                    // Check if x and y index are within array length
                    if (x < 0 || y < 0 || x >= _gridData.GetLength(0) || y >= _gridData.GetLength(1))
                        continue;

                    if (x < randomPos.x || y < randomPos.y || x >= randomPos.x + room.Width || y >= randomPos.y + room.Length)
                    {
                        _gridData[x, y] = CellState.NextToRoom;
                    }
                    else
                    {
                        _gridData[x, y] = CellState.Room;
                    }
                }
            }
            
            // the room is placed so return success
            return true;
        }

        return false;
    }
    
    private void RenderDungeon()
    {
        var meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = mat;
        
        var meshFilter = gameObject.AddComponent<MeshFilter>();

        var mesh = new Mesh();
        var verts = new List<Vector3>();
        var tris = new List<int>();
        var uvs = new List<Vector2>();

        // var i = 0;
        for (var x = 0; x < _gridSizeX; x++)
        {
            for (var y = 0; y < _gridSizeY; y++)
            {
                if (_gridData[x, y] != CellState.Room) continue;
                
                //Floor
                var floorI = verts.Count;
                verts.Add(new Vector3(x, 0, y));
                verts.Add(new Vector3(x, 0, y + 1));
                verts.Add(new Vector3(x + 1, 0, y + 1));
                verts.Add(new Vector3(x + 1, 0, y));
                    
                tris.Add(floorI);
                tris.Add(floorI + 1);
                tris.Add(floorI + 2);
                tris.Add(floorI);
                tris.Add(floorI + 2);
                tris.Add(floorI + 3);
                    
                uvs.Add( new Vector2(0, 0));
                uvs.Add( new Vector2(0, 2));
                uvs.Add( new Vector2(1, 2));
                uvs.Add( new Vector2(1, 0));
                
                // Ceiling
                var ceilI = verts.Count;
                verts.Add(new Vector3(x, 2, y + 1));
                verts.Add(new Vector3(x + 1, 2, y + 1));
                verts.Add(new Vector3(x + 1, 2, y));
                verts.Add(new Vector3(x, 2, y));
                
                tris.Add(ceilI + 2);
                tris.Add(ceilI + 1);
                tris.Add(ceilI);
                
                tris.Add(ceilI + 3);
                tris.Add(ceilI + 2);
                tris.Add(ceilI);
                
                uvs.Add( new Vector2(0, 0));
                uvs.Add( new Vector2(0, 2));
                uvs.Add( new Vector2(1, 2));
                uvs.Add( new Vector2(1, 0));

                // North wall
                var hasNorthWall = y + 1 >= _gridSizeY || _gridData[x, y + 1] != CellState.Room;
                if (hasNorthWall)
                {
                    var nWallI = verts.Count;
                    verts.Add(new Vector3(x, 0, y + 1));
                    verts.Add(new Vector3(x, 2, y + 1));
                    verts.Add(new Vector3(x + 1, 2, y + 1));
                    verts.Add(new Vector3(x + 1, 0, y + 1));
                    
                    tris.Add(nWallI);
                    tris.Add(nWallI + 1);
                    tris.Add(nWallI + 2);
                    tris.Add(nWallI);
                    tris.Add(nWallI + 2);
                    tris.Add(nWallI + 3);
                    
                    uvs.Add( new Vector2(0, 0));
                    uvs.Add( new Vector2(0, 2));
                    uvs.Add( new Vector2(1, 2));
                    uvs.Add( new Vector2(1, 0));
                }
                
                // East wall
                var hasEastWall = x + 1 >= _gridSizeX || _gridData[x + 1, y] != CellState.Room;
                if (hasEastWall)
                {
                    var eWallI = verts.Count;
                    verts.Add(new Vector3(x + 1, 0, y + 1));
                    verts.Add(new Vector3(x + 1, 2, y + 1));
                    verts.Add(new Vector3(x + 1, 2, y));
                    verts.Add(new Vector3(x + 1, 0, y));
                    
                    tris.Add(eWallI);
                    tris.Add(eWallI + 1);
                    tris.Add(eWallI + 2);
                    tris.Add(eWallI);
                    tris.Add(eWallI + 2);
                    tris.Add(eWallI + 3);
                    
                    uvs.Add( new Vector2(0, 0));
                    uvs.Add( new Vector2(0, 2));
                    uvs.Add( new Vector2(1, 2));
                    uvs.Add( new Vector2(1, 0));
                }
                
                // South wall
                var hasSouthWall = y - 1 < 0 || _gridData[x, y - 1] != CellState.Room;
                if (hasSouthWall)
                {
                    var eWallI = verts.Count;
                    verts.Add(new Vector3(x + 1, 0, y));
                    verts.Add(new Vector3(x + 1, 2,y));
                    verts.Add(new Vector3(x, 2,y));
                    verts.Add(new Vector3(x, 0,y));
                    
                    tris.Add(eWallI);
                    tris.Add(eWallI + 1);
                    tris.Add(eWallI + 2);
                    tris.Add(eWallI);
                    tris.Add(eWallI + 2);
                    tris.Add(eWallI + 3);
                    
                    uvs.Add( new Vector2(0, 0));
                    uvs.Add( new Vector2(0, 2));
                    uvs.Add( new Vector2(1, 2));
                    uvs.Add( new Vector2(1, 0));
                }
                
                // West wall
                var hasWestWall = x - 1 < 0 || _gridData[x - 1, y] != CellState.Room;
                if (hasWestWall)
                {
                    var eWallI = verts.Count;
                    verts.Add(new Vector3(x, 0, y));
                    verts.Add(new Vector3(x, 2, y));
                    verts.Add(new Vector3(x, 2, y + 1));
                    verts.Add(new Vector3(x, 0, y + 1));
                    
                    tris.Add(eWallI);
                    tris.Add(eWallI + 1);
                    tris.Add(eWallI + 2);
                    tris.Add(eWallI);
                    tris.Add(eWallI + 2);
                    tris.Add(eWallI + 3);
                    
                    uvs.Add( new Vector2(0, 0));
                    uvs.Add( new Vector2(0, 2));
                    uvs.Add( new Vector2(1, 2));
                    uvs.Add( new Vector2(1, 0));
                }
            }
        }

        var mc = gameObject.AddComponent<MeshCollider>();
        mc.sharedMesh = null;
        
        mesh.vertices = verts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        
        meshFilter.mesh = mesh;
        mc.sharedMesh = mesh;
    }
}
