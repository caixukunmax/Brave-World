using Godot;

namespace ClinetCSharp
{
    [GlobalClass]
    public partial class ItemDropPhysics : RigidBody2D
    {
        [Export] public float InitialArcForce { get; set; } = 300.0f;
        [Export] public float ArcAngleRange { get; set; } = 45.0f;
        [Export] public bool RandomSpin { get; set; } = true;
        [Export] public bool AutoDespawn { get; set; } = false;
        [Export] public float DespawnDelay { get; set; } = 5.0f;

        private bool _isSettled = false;
        private float _settleTimer = 0.0f;
        private const float SettleThreshold = 0.1f;

        [Signal]
        public delegate void ItemLandedEventHandler();

        [Signal]
        public delegate void ItemSettledEventHandler();

        public override async void _Ready()
        {
            GravityScale = 1.0f;

            var mat = new PhysicsMaterial();
            mat.Bounce = 0.6f;
            mat.Friction = 0.5f;
            PhysicsMaterialOverride = mat;

            ContactMonitor = true;
            MaxContactsReported = 4;

            BodyEntered += OnBodyEntered;

            await ToSignal(GetTree(), "process_frame");
            DropWithArc(Vector2.Right);
        }

        public void DropWithArc(Vector2 direction)
        {
            var angleOffset = (float)GD.RandRange(-ArcAngleRange, ArcAngleRange);
            var forceVector = direction.Rotated(Mathf.DegToRad(angleOffset)) * InitialArcForce;
            forceVector.Y = -Mathf.Abs(forceVector.Y) * 0.8f;

            ApplyCentralImpulse(forceVector);

            if (RandomSpin)
                ApplyTorqueImpulse(GD.RandRange(-100, 100));

            GD.Print("[ItemDropPhysics] Drop started, impulse: " + forceVector);
        }

        public void DropStraight()
        {
            ApplyCentralImpulse(new Vector2(GD.RandRange(-20, 20), -50));
        }

        public override void _IntegrateForces(PhysicsDirectBodyState2D state)
        {
            if (!_isSettled)
            {
                var velocity = state.LinearVelocity;
                var angular = state.AngularVelocity;

                if (velocity.Length() < SettleThreshold && Mathf.Abs(angular) < SettleThreshold)
                {
                    _settleTimer += state.Step;
                    if (_settleTimer > 0.5f)
                    {
                        _isSettled = true;
                        EmitSignal(SignalName.ItemSettled);
                        GD.Print("[ItemDropPhysics] Item settled");

                        if (AutoDespawn)
                        {
                            var timer = GetTree().CreateTimer(DespawnDelay);
                            timer.Timeout += QueueFree;
                        }
                    }
                }
                else
                {
                    _settleTimer = 0.0f;
                }
            }
        }

        private void OnBodyEntered(Node body)
        {
            if (body.IsInGroup("ground") || body.IsClass("TileMap"))
            {
                SpawnImpactEffect();

                if (!_isSettled)
                {
                    EmitSignal(SignalName.ItemLanded);
                    GD.Print("[ItemDropPhysics] Item landed");
                }
            }
        }

        private void SpawnImpactEffect()
        {
            // Spawn dust particles effect
        }
    }
}
