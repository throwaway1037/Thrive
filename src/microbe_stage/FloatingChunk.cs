﻿using System;
using System.Collections.Generic;
using Godot;
using Newtonsoft.Json;

/// <summary>
///   Script for the floating chunks (cell parts, rocks, hazards)
/// </summary>
[JSONAlwaysDynamicType]
[SceneLoadedClass("res://src/microbe_stage/FloatingChunk.tscn", UsesEarlyResolve = false)]
public class FloatingChunk : RigidBody, ISpawned, ISaveLoadedTracked, IEngulfable
{
    [Export]
    [JsonProperty]
    public PackedScene GraphicsScene = null!;

    /// <summary>
    ///   If this is null, a sphere shape is used as a default for collision detections.
    /// </summary>
    [Export]
    [JsonProperty]
    public ConvexPolygonShape? ConvexPhysicsMesh;

    /// <summary>
    ///   The node path to the mesh of this chunk
    /// </summary>
    public string? ModelNodePath;

    /// <summary>
    ///   Used to check if a microbe wants to engulf this
    /// </summary>
    private HashSet<Microbe> touchingMicrobes = new();

    private MeshInstance? chunkMesh;

    [JsonProperty]
    private bool isDissolving;

    [JsonProperty]
    private bool isFadingParticles;

    [JsonProperty]
    private float particleFadeTimer;

    [JsonProperty]
    private float dissolveEffectValue;

    [JsonProperty]
    private bool isParticles;

    [JsonProperty]
    private float elapsedSinceProcess;

    public int DespawnRadiusSquared { get; set; }

    [JsonIgnore]
    public Node EntityNode => this;

    [JsonIgnore]
    public GeometryInstance EntityGraphics => chunkMesh!;

    [JsonIgnore]
    public Material EntityMaterial => chunkMesh?.MaterialOverride!;

    /// <summary>
    ///   Determines how big this chunk is for engulfing calculations. Set to &lt;= 0 to disable
    /// </summary>
    public float Size { get; set; } = -1.0f;

    /// <summary>
    ///   Compounds this chunk contains, and vents
    /// </summary>
    public CompoundBag? ContainedCompounds { get; set; }

    /// <summary>
    ///   How much of each compound is vented per second
    /// </summary>
    public float VentPerSecond { get; set; } = 5.0f;

    /// <summary>
    ///   If true this chunk is destroyed when all compounds are vented
    /// </summary>
    public bool Dissolves { get; set; }

    /// <summary>
    ///   If > 0 applies damage to a cell on touch
    /// </summary>
    public float Damages { get; set; }

    /// <summary>
    ///   When true, the chunk will despawn when the despawn timer finishes
    /// </summary>
    public bool UsesDespawnTimer { get; set; }

    /// <summary>
    ///   How much time has passed since a chunk that uses this timer has been spawned
    /// </summary>
    [JsonProperty]
    public float DespawnTimer { get; private set; }

    /// <summary>
    ///   If true this gets deleted when a cell touches this
    /// </summary>
    public bool DeleteOnTouch { get; set; }

    public float Radius { get; set; }

    public float ChunkScale { get; set; }

    /// <summary>
    ///   The name of kind of damage type this chunk inflicts. Default is "chunk".
    /// </summary>
    public string DamageType { get; set; } = "chunk";

    public bool IsLoadedFromSave { get; set; }

    [JsonIgnore]
    public AliveMarker AliveMarker { get; } = new();

    [JsonProperty]
    public EngulfmentStep CurrentEngulfmentStep { get; set; }

    [JsonProperty]
    public EntityReference<Microbe> HostileEngulfer { get; private set; } = new();

    [JsonIgnore]
    public Enzyme? RequisiteEnzymeToDigest => null;

    /// <summary>
    ///   This is both the digestion and dissolve effect progress value for now.
    /// </summary>
    [JsonIgnore]
    public float DigestionProgress
    {
        get => dissolveEffectValue;
        set
        {
            dissolveEffectValue = Mathf.Clamp(value, 0.0f, 1.0f);
            UpdateDissolveEffect();
        }
    }

    /// <summary>
    ///   Grabs data from the type to initialize this
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Doesn't initialize the graphics scene which needs to be set separately
    ///   </para>
    /// </remarks>
    public void Init(ChunkConfiguration chunkType, string? modelPath)
    {
        // Grab data
        VentPerSecond = chunkType.VentAmount;
        Dissolves = chunkType.Dissolves;
        Size = chunkType.Size;
        Damages = chunkType.Damages;
        DeleteOnTouch = chunkType.DeleteOnTouch;
        DamageType = string.IsNullOrEmpty(chunkType.DamageType) ? "chunk" : chunkType.DamageType;

        Mass = chunkType.Mass;

        // These are stored for saves to work
        Radius = chunkType.Radius;
        ChunkScale = chunkType.ChunkScale;

        ModelNodePath = modelPath;

        // Copy compounds to vent
        if (chunkType.Compounds?.Count > 0)
        {
            // Capacity is set to 0 so that no compounds can be added
            // the normal way to the chunk
            ContainedCompounds = new CompoundBag(0);

            foreach (var entry in chunkType.Compounds)
            {
                ContainedCompounds.Compounds.Add(entry.Key, entry.Value.Amount);
            }
        }
    }

    /// <summary>
    ///   Reverses the action of Init back to a ChunkConfiguration
    /// </summary>
    /// <returns>The reversed chunk configuration</returns>
    public ChunkConfiguration CreateChunkConfigurationFromThis()
    {
        var config = default(ChunkConfiguration);

        config.VentAmount = VentPerSecond;
        config.Dissolves = Dissolves;
        config.Size = Size;
        config.Damages = Damages;
        config.DeleteOnTouch = DeleteOnTouch;
        config.Mass = Mass;
        config.DamageType = DamageType;

        config.Radius = Radius;
        config.ChunkScale = ChunkScale;

        // Read graphics data set by the spawn function
        config.Meshes = new List<ChunkConfiguration.ChunkScene>();

        var item = new ChunkConfiguration.ChunkScene
        {
            LoadedScene = GraphicsScene, ScenePath = GraphicsScene.ResourcePath, SceneModelPath = ModelNodePath,
            LoadedConvexShape = ConvexPhysicsMesh, ConvexShapePath = ConvexPhysicsMesh?.ResourcePath,
        };

        config.Meshes.Add(item);

        if (ContainedCompounds?.Compounds.Count > 0)
        {
            config.Compounds = new Dictionary<Compound, ChunkConfiguration.ChunkCompound>();

            foreach (var entry in ContainedCompounds)
            {
                config.Compounds.Add(entry.Key, new ChunkConfiguration.ChunkCompound { Amount = entry.Value });
            }
        }

        return config;
    }

    public override void _Ready()
    {
        var graphicsNode = GraphicsScene.Instance();
        GetNode("NodeToScale").AddChild(graphicsNode);

        if (string.IsNullOrEmpty(ModelNodePath))
        {
            if (graphicsNode.IsClass("MeshInstance"))
            {
                chunkMesh = (MeshInstance)graphicsNode;
            }
            else if (graphicsNode.IsClass("Particles"))
            {
                isParticles = true;
            }
            else
            {
                throw new Exception("Invalid class");
            }
        }
        else
        {
            chunkMesh = graphicsNode.GetNode<MeshInstance>(ModelNodePath);
        }

        if (chunkMesh == null && !isParticles)
            throw new InvalidOperationException("Can't make a chunk without graphics scene");

        InitPhysics();
    }

    public void ProcessChunk(float delta, CompoundCloudSystem compoundClouds)
    {
        if (CurrentEngulfmentStep != EngulfmentStep.NotEngulfed)
            return;

        if (isDissolving)
            HandleDissolving(delta);

        if (isFadingParticles)
        {
            particleFadeTimer -= delta;

            if (particleFadeTimer <= 0)
            {
                this.DestroyDetachAndQueueFree();
            }
        }

        elapsedSinceProcess += delta;

        // Skip some of our more expensive operations if not enough time has passed
        // This doesn't actually seem to have that much effect with reasonable chunk counts... but doesn't seem
        // to hurt either, so for the future I think we should keep this -hhyyrylainen
        if (elapsedSinceProcess < Constants.FLOATING_CHUNK_PROCESS_INTERVAL)
            return;

        VentCompounds(elapsedSinceProcess, compoundClouds);

        if (UsesDespawnTimer)
            DespawnTimer += elapsedSinceProcess;

        // Check contacts
        foreach (var microbe in touchingMicrobes)
        {
            // TODO: is it possible that this throws the disposed exception?
            if (microbe.Dead)
                continue;

            // Damage
            if (Damages > 0)
            {
                if (DeleteOnTouch)
                {
                    microbe.Damage(Damages, DamageType);
                }
                else
                {
                    microbe.Damage(Damages * elapsedSinceProcess, DamageType);
                }
            }

            if (DeleteOnTouch)
            {
                DissolveOrRemove();
                break;
            }
        }

        if (DespawnTimer > Constants.DESPAWNING_CHUNK_LIFETIME)
            DissolveOrRemove();

        elapsedSinceProcess = 0;
    }

    public void PopImmediately(CompoundCloudSystem compoundClouds)
    {
        // Vent all remaining compounds immediately
        if (ContainedCompounds != null)
        {
            var pos = Translation;

            var keys = new List<Compound>(ContainedCompounds.Compounds.Keys);

            foreach (var compound in keys)
            {
                var amount = ContainedCompounds.GetCompoundAmount(compound);

                if (amount < MathUtils.EPSILON)
                    continue;

                VentCompound(pos, compound, amount, compoundClouds);
            }
        }

        this.DestroyDetachAndQueueFree();
    }

    public void OnDestroyed()
    {
        AliveMarker.Alive = false;
    }

    public Dictionary<Compound, float> CalculateDigestibleCompounds()
    {
        var result = new Dictionary<Compound, float>();

        if (ContainedCompounds == null)
            return result;

        foreach (var entry in ContainedCompounds)
        {
            result.Add(entry.Key, entry.Value / Constants.CHUNK_ENGULF_COMPOUND_DIVISOR);
        }

        return result;
    }

    public void OnEngulfed()
    {
    }

    public void OnEjected()
    {
        if (DigestionProgress > 0)
        {
            // Just dissolve this chunk entirely (as it's already somehow broken down by digestion)
            DissolveOrRemove();
        }
    }

    /// <summary>
    ///   Vents compounds if this is a chunk that contains compounds
    /// </summary>
    private void VentCompounds(float delta, CompoundCloudSystem compoundClouds)
    {
        if (ContainedCompounds == null)
            return;

        var pos = Translation;

        var keys = new List<Compound>(ContainedCompounds.Compounds.Keys);

        // Loop through all the compounds in the storage bag and eject them
        bool vented = false;
        foreach (var compound in keys)
        {
            var amount = ContainedCompounds.GetCompoundAmount(compound);

            if (amount <= 0)
                continue;

            var got = ContainedCompounds.TakeCompound(compound, VentPerSecond * delta);

            if (got > MathUtils.EPSILON)
            {
                VentCompound(pos, compound, got, compoundClouds);
                vented = true;
            }
        }

        // If you did not vent anything this step and the venter component
        // is flagged to dissolve you, dissolve you
        if (!vented && Dissolves)
        {
            isDissolving = true;
        }
    }

    private void VentCompound(Vector3 pos, Compound compound, float amount, CompoundCloudSystem compoundClouds)
    {
        compoundClouds.AddCloud(compound, amount * Constants.CHUNK_VENT_COMPOUND_MULTIPLIER, pos);
    }

    /// <summary>
    ///   Handles the dissolving effect for the chunks when they run out of compounds.
    /// </summary>
    private void HandleDissolving(float delta)
    {
        if (chunkMesh == null)
            throw new InvalidOperationException("Chunk without a mesh can't dissolve");

        // Disable collisions
        CollisionLayer = 0;
        CollisionMask = 0;

        DigestionProgress += delta * Constants.FLOATING_CHUNKS_DISSOLVE_SPEED;

        if (DigestionProgress >= 1)
        {
            this.DestroyDetachAndQueueFree();
        }
    }

    private void UpdateDissolveEffect()
    {
        if (chunkMesh == null)
            throw new InvalidOperationException("Chunk without a mesh can't dissolve");

        var material = (ShaderMaterial)chunkMesh.MaterialOverride;
        material.SetShaderParam("dissolveValue", dissolveEffectValue);
    }

    private void InitPhysics()
    {
        // Apply physics shape
        var shape = GetNode<CollisionShape>("CollisionShape");

        if (ConvexPhysicsMesh == null)
        {
            var sphereShape = new SphereShape { Radius = Radius };
            shape.Shape = sphereShape;
        }
        else
        {
            if (chunkMesh == null)
                throw new InvalidOperationException("Can't use convex physics shape without mesh for chunk");

            shape.Shape = ConvexPhysicsMesh;
            shape.Transform = chunkMesh.Transform;
        }

        // Needs physics callback when this is engulfable or damaging
        if (Damages > 0 || DeleteOnTouch || Size > 0)
        {
            ContactsReported = Constants.DEFAULT_STORE_CONTACTS_COUNT;
            Connect("body_shape_entered", this, nameof(OnContactBegin));
            Connect("body_shape_exited", this, nameof(OnContactEnd));
        }
    }

    private void OnContactBegin(int bodyID, Node body, int bodyShape, int localShape)
    {
        _ = bodyID;
        _ = localShape;

        if (body is Microbe microbe)
        {
            // Can't engulf with a pilus
            if (microbe.IsPilus(microbe.ShapeFindOwner(bodyShape)))
                return;

            var target = microbe.GetMicrobeFromShape(bodyShape);
            if (target != null)
                touchingMicrobes.Add(target);
        }
    }

    private void OnContactEnd(int bodyID, Node body, int bodyShape, int localShape)
    {
        _ = bodyID;
        _ = localShape;

        if (body is Microbe microbe)
        {
            var shapeOwner = microbe.ShapeFindOwner(bodyShape);

            // This can happen when a microbe unbinds while also touching a floating chunk
            // TODO: Do something more elegant to stop the error messages in the log
            if (shapeOwner == 0)
            {
                touchingMicrobes.Remove(microbe);
                return;
            }

            // This might help in a case where the cell is touching with both a pilus and non-pilus part
            if (microbe.IsPilus(shapeOwner))
                return;

            var target = microbe.GetMicrobeFromShape(bodyShape);

            if (target != null)
                touchingMicrobes.Remove(target);
        }
    }

    private void DissolveOrRemove()
    {
        if (Dissolves)
        {
            isDissolving = true;
        }
        else if (isParticles && !isFadingParticles)
        {
            isFadingParticles = true;

            var particles = GetNode("NodeToScale").GetChild<Particles>(0);

            // Disable collisions
            CollisionLayer = 0;
            CollisionMask = 0;

            particles.Emitting = false;
            particleFadeTimer = particles.Lifetime;
        }
        else if (!isParticles)
        {
            this.DestroyDetachAndQueueFree();
        }
    }
}
