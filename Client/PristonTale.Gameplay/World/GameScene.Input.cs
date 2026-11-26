using System.Threading.Channels;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PristonTale.Client.Net;
using PristonTale.Client.Net.Presence;
using PristonTale.Engine3D.Diagnostics;
using PristonTale.Engine3D.Formats;
using PristonTale.Engine3D.Physics;
using PristonTale.Engine3D.Rendering;
using PristonTale.Gameplay.Combat;
using PristonTale.Gameplay.UI.Widgets;
using PristonTale.Gameplay.Audio;
using PristonTale.Gameplay.World.Remote;
using PristonTale.Gameplay.World.Streaming;
using PristonTale.Simulation.Characters;
using PristonTale.Simulation.Physics;
using PristonTale.Simulation.World;
using PristonTale.Simulation.World.Motion;
using NumVector3 = System.Numerics.Vector3;
using PristonTale.Contracts.Motion;
using PristonTale.Contracts.World;
using PristonTale.Contracts.Combat;
using PristonTale.Engine3D;
using PristonTale.Assets;
using PristonTale.Contracts.Characters;
using NumVector2 = System.Numerics.Vector2;

namespace PristonTale.Gameplay.World;

// The local player's input: moving, picking loot and targets, approaching NPCs and the attack it shows.
public sealed partial class GameScene
{
    internal Vector2 MobileMove { get; set; }
    internal Vector2 MobileOrbit { get; set; }
    internal float MobileZoom { get; set; } // ETERVALE_MOBILE_INPUT_V24
    internal bool MobileActionOnly { get; set; }
    internal Point? MobileTap { get; set; } // ETERVALE_MOBILE_TARGET_LOOT_V28
    internal bool MobileSelectNext { get; set; }
    internal bool MobileLootNearest { get; set; }
    internal bool MobileTalkNearest { get; set; } // ETERVALE_MOBILE_HUD_EDITOR_V33 // ETERVALE_MOBILE_UI_V32
    private bool _mobileMoving;

    private void UpdateMobileMovement(MouseState mouse)
    {
        // Mouse/skill input and modal locks take priority. Releasing the stick only stops a stick-owned walk.
        var input = MobileMove;
        var wanted = input.LengthSquared() > .0001f && CameraInputEnabled && !MovementLocked && !KeyboardCaptured
            && !SelfCasting && !SelfStunned && !_selfAttacking && !_playerController.LocomotionFrozen && _pendingCast is null
            && mouse.LeftButton == ButtonState.Released && mouse.RightButton == ButtonState.Released;
        if (!wanted)
        {
            if (_mobileMoving)
            {
                _mobileMoving = false;
                // A new mouse click or cast may already own the destination.
                if (mouse.LeftButton == ButtonState.Released && mouse.RightButton == ButtonState.Released && !SelfCasting && _pendingCast is null && _attackInput.TargetId == 0 && _npcTargetId == 0)
                {
                    StopPredictedMovement();
                    _reports.Request();
                }
            }
            return;
        }
        var forward = new Vector2(_camera.FlatForward.X, _camera.FlatForward.Z);
        if (forward.LengthSquared() < .0001f) return;
        forward.Normalize();
        var right = new Vector2(-forward.Y, forward.X);
        var direction = forward * input.Y + right * input.X;
        if (direction.LengthSquared() < .0001f) return;
        direction.Normalize();
        if (!_mobileMoving)
        {
            EndSkillTrace();
            CancelPendingCast();
            ClearAttackTarget();
            _lootInteraction?.Cancel();
            _holdCursorMovement = false;
            _followCursor = false;
            _reports.Request();
        }
        _mobileMoving = true;
        _moveTarget = _playerBody.PlanarPosition + direction * Math.Max(PlayerRadius * 4f, MovementSpeed(_runMode) * .25f);
        _hasMoveTarget = true;
    }

    private void UpdatePlayerInput(GameTime gameTime)
    {
        if (_renderer == null || _activeField == null || _camera == null || _playerBody == null)
        {
            return;
        }

        var mouse = Mouse.GetState();
        if (_pendingWarp is not null)
        {
            _lastMouseState = mouse;
            return;
        }

        if (_playerController.IsDead)
        {
            _lastMouseState = mouse;
            return;
        }

        // ETERVALE_MOBILE_TARGET_LOOT_V28: processa seleção e loot somente quando o cenário está livre.
        if (OperatingSystem.IsAndroid() && CameraInputEnabled && !MovementLocked && !KeyboardCaptured)
        {
            if (MobileSelectNext && _remote is not null)
            {
                var target=_remote.NextMobileMonster(_targetSelection.FrameId,_playerBody.Position);
                if (target!=0)
                {
                    if (_attackInput.TargetId!=0 && _attackInput.TargetId!=target) ClearAttackTarget();
                    _targetSelection.Trace(target);
                }
            }
            if (MobileTap is Point screenPoint) SelectMobileWorldPoint(screenPoint);
        }
        // ETERVALE_MOBILE_UI_V32: recolha explícita sem clique ou movimento indiscriminado.
        if (OperatingSystem.IsAndroid() && CanProcessMobileLoot(MobileLootNearest, MovementLocked, KeyboardCaptured, CameraInputEnabled))
        {
            TryMobileLootNearest();
        }
        if (OperatingSystem.IsAndroid() && MobileTalkNearest && !MovementLocked
            && !KeyboardCaptured && CameraInputEnabled && !WorldClicksBlocked)
            TryMobileTalkNearest(); // ETERVALE_MOBILE_HUD_EDITOR_V33
        if (MovementLocked && (_hasMoveTarget || _followCursor))
        {
            _followCursor = false;
            _holdCursorMovement = false;
            StopPredictedMovement();
            _reports.Request();
        }

        if (_mobileMoving && (mouse.LeftButton == ButtonState.Pressed || mouse.RightButton == ButtonState.Pressed))
        {
            _mobileMoving = false;
            StopPredictedMovement();
            _reports.Request();
        }
        if (mouse.LeftButton == ButtonState.Released)
        {
            _holdCursorMovement = false;
        }

        if (!WorldClicksBlocked && RightAttackEnabled && mouse.RightButton == ButtonState.Pressed
            && _lastMouseState.RightButton == ButtonState.Released && TryPickAttackTarget(mouse, rightButton: true))
        {
            _hasMoveTarget = false;
        }
        else if (!WorldClicksBlocked && !RightAttackEnabled && mouse.RightButton == ButtonState.Pressed
            && _lastMouseState.RightButton == ButtonState.Released && !SelfCasting)
        {
            EndSkillTrace();
            TryCastSkillClick(mouse, rightButton: true);
        }

        UpdateActionKeys(gameTime);
        UpdateRunMode(mouse);
        if (!WorldClicksBlocked && mouse.LeftButton == ButtonState.Pressed && _lastMouseState.LeftButton == ButtonState.Released)
        {
            var nowMs = gameTime.TotalGameTime.TotalMilliseconds;
            var doubleClick = (nowMs - _lastLeftClickMs) <= DoubleClickMs;
            _lastLeftClickMs = nowMs;
            _followCursor = false;
            _holdCursorMovement = false;
            EndSkillTrace();
            if (!MobileActionOnly && TryPickLoot(mouse))
            {
            }
            else if (!MobileActionOnly && !SelfCasting && TryCastSkillClick(mouse, rightButton: false))
            {
            }
            else if (TryPickAttackTarget(mouse))
            {
                _hasMoveTarget = false;
            }
            else if (!MobileActionOnly && SetMoveTarget(mouse))
            {
                // A ground click during a skill motion is kept: the walk starts once the skill ends.
                CancelPendingCast();
                ClearAttackTarget();
                _lootInteraction.Cancel();
                _followCursor = doubleClick;
                _holdCursorMovement = true;
                _reports.Request();
            }
        }

        if (_attackInput.Release(mouse.LeftButton == ButtonState.Pressed, mouse.RightButton == ButtonState.Pressed) is { } release)
        {
            SendAttackIntent(release);
        }

        UpdateCharge(mouse);
        EndSkillMotion();
        UpdateSkillTrace(mouse);
        UpdatePendingCast();
        UpdateNpcApproach();
        UpdateReachedPlayer();
        _lootInteraction?.Update(_groundLoot, _playerBody.Position, _serverPlanarPosition, Owner);
        UpdateAttackPresentation(gameTime);
        if ((_holdCursorMovement || _followCursor) && !WorldClicksBlocked && !SelfCasting)
        {
            SetMoveTarget(mouse);
        }

        if (OperatingSystem.IsAndroid()) UpdateMobileMovement(mouse);
        if (_hasMoveTarget && !_playerController.LocomotionFrozen && !_selfAttacking && !SelfCasting && !SelfStunned)
        {
            var current = _playerBody.PlanarPosition;
            var running = _runMode;

            var step = DestinationMovement.Advance(
                new System.Numerics.Vector2(current.X, current.Y),
                new System.Numerics.Vector2(_moveTarget.X, _moveTarget.Y),
                MovementSpeed(running),
                (float)gameTime.ElapsedGameTime.TotalSeconds);
            var proposed = new Vector2(step.Position.X, step.Position.Y);
            var slid = MoveWithCollision(current, proposed - current, _playerBody.Height + BodyProbeHeight);
            CommitGroundMovement(ResolveEntityCollision(current, slid));
            // Following the cursor ends where the player is blocked, as in the original.
            if (_followCursor && proposed != current && Vector2.DistanceSquared(_playerBody.PlanarPosition, current) < 1e-4f)
            {
                _followCursor = false;
            }

            if (step.Direction != System.Numerics.Vector2.Zero)
            {
                _playerForward = new Vector3(step.Direction.X, 0f, step.Direction.Y);
            }

            ApplyPlayerWorld();
            var arrived = DestinationMovement.HasArrived(
                new System.Numerics.Vector2(_playerBody.PlanarPosition.X, _playerBody.PlanarPosition.Y),
                new System.Numerics.Vector2(_moveTarget.X, _moveTarget.Y));
            if (arrived)
            {
                _hasMoveTarget = false;
                _positionCorrection = Vector2.Zero;
                ApplyPlayerCommand(_playerController.ApplyLocomotion(MotionCode.Stand));
            }
            else
            {
                var motion = running ? MotionCode.Run : MotionCode.Walk;
                ApplyPlayerCommand(_playerController.ApplyLocomotion(motion));
            }
        }

        _lastMouseState = mouse;
    }

    // The floor query takes the highest surface at or below the reference plus a step, so the reference is the drop's
    // own height; the player's height is only the fallback for a field the server holds no geometry for.
    private bool LootFloorAt(float x, float referenceHeight, float z, out float height) =>
        TryGetFloorHeightNear(new Vector3(x, 0, z), referenceHeight, out height)
        || TryGetFloorHeightNear(new Vector3(x, 0, z), _playerBody.Height + GroundTraversal.VisualOffset, out height);

    // ETERVALE_MOBILE_TARGET_LOOT_V28: o item usa a mesma aproximação/validação de TryPickLoot.
    // O monstro recebe frame de seleção, sem ataque nem clique no chão.
    private void SelectMobileWorldPoint(Point point)
    {
        var mouse = new MouseState(point.X,point.Y,0,
            ButtonState.Released,ButtonState.Released,ButtonState.Released,
            ButtonState.Released,ButtonState.Released);
        if (TryPickLoot(mouse)) return;
        if (_remote is null || !TryCursorRay(mouse,out var ray)
            || !_remote.TryPick(ray,_remote.SelfEntityId,out var id,out var kind)) return;
        if (kind==EntityKind.Monster || kind==EntityKind.Player && Fights(id))
        {
            if (_attackInput.TargetId!=0 && _attackInput.TargetId!=id) ClearAttackTarget();
            _targetSelection.Trace(id);
        }
        else if (kind==EntityKind.Npc || kind==EntityKind.Player)
        {
            // Dialogo e comercio seguem o fluxo normal da seleção por rato.
            TryPickAttackTarget(mouse);
        }
    }

    // ETERVALE_MOBILE_UI_V32: encontra apenas drops próximos, e aproveita
    // o mesmo caminho de aproximação e a mesma validação usados pelo rato.
    // MobileLootNearest is a dedicated HUD command, not a mouse click. The virtual cursor may still be over another widget.
    internal static bool CanProcessMobileLoot(bool requested, bool movementLocked, bool keyboardCaptured, bool cameraInputEnabled) =>
        requested && !movementLocked && !keyboardCaptured && cameraInputEnabled;

    private bool TryMobileLootNearest()
    {
        if (_groundLoot is null || _lootInteraction is null || _lootInteraction.Busy || _playerBody is null)
            return false;
        if (!_groundLoot.TryFindNearest(PlayerFeet,240f,out var id,out var position))
            return false;
        if (!GroundTraversal.CanReachSurface(PlayerFeet,position,_groundFloorQuery))
            return false;
        ClearAttackTarget();
        _npcTargetId=0;
        _lootInteraction.Select(id);
        _moveTarget=new Vector2(position.X,position.Z);
        _hasMoveTarget=true;
        _reports.Request();
        return true;
    }

    // ETERVALE_MOBILE_HUD_EDITOR_V33: o NPC aproxima-se e abre diálogo pelo fluxo normal.
    // Botão FALAR não envia um clique de rato nem uma ordem arbitrária ao servidor.
    private bool TryMobileTalkNearest()
    {
        if(_remote is null || _playerBody is null || SelfCasting)return false;
        var id=_targetSelection.FrameId;
        if(id==0 || !_remote.TryGetMobileTarget(id,out var kind) || kind!=EntityKind.Npc)
            id=_remote.NearestMobileNpc(_playerBody.Position,640f);
        if(id==0)return false;
        _lootInteraction?.Cancel();
        ClearAttackTarget();
        _npcTargetId=id;
        _approach=ApproachKind.Npc;
        _npcMoveSent=false;
        _targetSelection.Trace(id);
        StopPredictedMovement();
        return true;
    }

    private bool TryPickLoot(MouseState mouse)
    {
        if (_groundLoot is null || _lootInteraction.Busy || !TryCursorRay(mouse, out var ray)
            || !_groundLoot.Pick(mouse.Position, ray, out var id, out var position, ShowAllDropLabels))
        {
            return false;
        }

        var feet = PlayerFeet;
        if (!GroundTraversal.CanReachSurface(feet, position, _groundFloorQuery))
        {
            return true;
        }

        ClearAttackTarget();
        _npcTargetId = 0;
        _lootInteraction.Select(id);
        _moveTarget = new Vector2(position.X, position.Z);
        _hasMoveTarget = true;
        _reports.Request();
        return true;
    }

    private bool SetMoveTarget(MouseState mouse)
    {
        if (!TryCursorRay(mouse, out var ray))
        {
            return false;
        }

        if (!TryRaycastFloor(ray, out var point))
        {
            return false;
        }

        var feet = PlayerFeet;
        if (!GroundTraversal.CanReachSurface(feet, point, _groundFloorQuery))
        {
            return false;
        }

        var destination = new Vector2(point.X, point.Z);
        var current = _playerBody.PlanarPosition;
        if (DestinationMovement.WithinDistance(new NumVector2(current.X, current.Y), new NumVector2(destination.X, destination.Y), PlayerRadius))
        {
            if (mouse.LeftButton == ButtonState.Pressed)
            {
                var direction = _moveTarget - current;
                if (direction.LengthSquared() <= 0.0001f)
                {
                    direction = new Vector2(_playerForward.X, _playerForward.Z);
                }

                if (direction.LengthSquared() > 0.0001f)
                {
                    _moveTarget = current + Vector2.Normalize(direction) * (PlayerRadius * 3f);
                    _hasMoveTarget = true;
                    return true;
                }
            }

            var wasMoving = _hasMoveTarget;
            _moveTarget = current;
            _hasMoveTarget = false;
            if (!_playerController.LocomotionFrozen && !_selfAttacking && !SelfCasting && !SelfStunned)
            {
                ApplyPlayerCommand(_playerController.ApplyLocomotion(MotionCode.Stand));
            }

            if (wasMoving)
            {
                _reports.Request();
            }

            return true;
        }

        _moveTarget = destination;
        _hasMoveTarget = true;
        return true;
    }

    private bool TryCursorRay(MouseState mouse, out Ray ray)
    {
        return CursorPicking.TryCreateRay(
            _graphicsDevice.Viewport,
            _camera.Projection,
            _camera.View,
            _camera.Position,
            mouse.Position,
            out ray);
    }

    private bool TryPickAttackTarget(MouseState mouse, bool rightButton = false)
    {
        if (_remote == null)
        {
            return false;
        }
        ulong entityId;
        EntityKind kind;
        if (MobileActionOnly)
        {
            entityId = _targetSelection.FrameId;
            if (!_remote.TryGetMobileTarget(entityId, out kind) || kind!=EntityKind.Monster)
            {
                // ETERVALE_MOBILE_TARGET_LOOT_V28: ATACAR seleciona um alvo sem depender do cursor central.
                entityId = _remote.NextMobileMonster(0,_playerBody.Position);
                if (entityId==0 || !_remote.TryGetMobileTarget(entityId,out kind)) return false;
            }
        }
        else if (!TryCursorRay(mouse, out var ray) || !_remote.TryPick(ray, _remote.SelfEntityId, out entityId, out kind))
        {
            return false;
        }
        if (MobileActionOnly && (kind == EntityKind.Npc || kind == EntityKind.Player && !Fights(entityId))) return false;
        // A seller is approached and its shop opened as an NPC's dialog is; another player is approached for the buttons
        // under its portrait, unless the castle's battle zone lets the two fight (the server decides).
        if (kind is EntityKind.Npc || kind == EntityKind.Player && !Fights(entityId))
        {
            _lootInteraction.Cancel();
            ClearAttackTarget();
            _npcTargetId = entityId;
            _approach = kind == EntityKind.Npc ? ApproachKind.Npc : _remote.ShopSigns.ContainsKey(entityId) ? ApproachKind.Seller : ApproachKind.Player;
            _npcMoveSent = false;
            _targetSelection.Trace(entityId);
            StopPredictedMovement();
            return true;
        }
        if (kind is not (EntityKind.Monster or EntityKind.Player))
        {
            return false;
        }

        _npcTargetId = 0;
        _lootInteraction.Cancel();
        _targetSelection.Trace(entityId);

        var automatic = ShiftHeld(Keyboard.GetState());
        SendAttackIntent(_attackInput.Press(entityId, automatic, rightButton));
        return true;
    }

    private void SendAttackIntent(AttackRequest request)
    {
        if (_playChannel is null)
        {
            return;
        }

        SendUpstream(_playChannel.SendAttackAsync(request.TargetId, request.Sustained, _presenceCts.Token, request.Sequence), "attack");
    }

    // Like the original client's attack cancel, this drops the trace but keeps the target frame open; the server stops
    // chasing the target too.
    private void ClearAttackTarget()
    {
        if (_attackInput.Stop() is { } stop)
        {
            SendAttackIntent(stop);
        }

        _targetSelection.ClearTrace();
        _npcTargetId = 0;
        _npcMoveSent = false;
    }

    // Hover picking runs in Update so Draw stays free of scene queries.
    private void UpdateTargetSelection(GameTime gameTime)
    {
        ulong hovered = 0;
        var hoveredKind = EntityKind.None;
        if (_remote is not null && _camera is not null && !PointerCaptured && CameraInputEnabled && TryCursorRay(Mouse.GetState(), out var ray)
            && _remote.TryPick(ray, _remote.SelfEntityId, out var entityId, out var kind))
        {
            hovered = entityId;
            hoveredKind = kind;
        }

        _targetSelection.Hover(hovered);
        UpdateOutline(hovered, hoveredKind);
        _targetSelection.Update(gameTime.TotalGameTime.TotalMilliseconds, _targetLiveness);
        UpdateHoveredDrop();
    }

    // The drop under the cursor, by its label or its model: it names the label to highlight and turns the pointer into
    // the original's grabbing hand.
    private void UpdateHoveredDrop()
    {
        var mouse = Mouse.GetState();
        HoveredDropId = Guid.Empty;
        if (_groundLoot is not null && _camera is not null && !PointerCaptured && CameraInputEnabled && TryCursorRay(mouse, out var ray)
            && _groundLoot.Pick(mouse.Position, ray, out var id, out _, ShowAllDropLabels))
        {
            HoveredDropId = id;
        }
    }

    // Holding A names every drop in view, as in the original.
    private bool ShowAllDropLabels => !PointerCaptured && !KeyboardCaptured && Keyboard.GetState().IsKeyDown(Keys.A);

    private TargetLiveness QueryTargetLiveness(ulong entityId)
    {
        if (_remote is null || !_remote.TryGetTarget(entityId, out _, out var dead))
        {
            return TargetLiveness.Missing;
        }

        return dead ? TargetLiveness.Dead : TargetLiveness.Alive;
    }

    // During a skill motion the walk is dropped but the skill clip keeps playing.
    private void StopPredictedMovement()
    {
        _hasMoveTarget = false;
        _positionCorrection = Vector2.Zero;
        if (!SelfCasting)
        {
            ApplyPlayerCommand(_playerController.ApplyLocomotion(MotionCode.Stand));
        }
    }

    private void UpdateNpcApproach()
    {
        if (_npcTargetId == 0 || _remote is null || SelfCasting)
        {
            return;
        }

        if (_playerController.IsDead || !_remote.TryGetTarget(_npcTargetId, out var position, out _))
        {
            ClearAttackTarget();
            StopPredictedMovement();
            return;
        }

        var target = new Vector2(position.X, position.Z);
        var decision = NpcApproach.Evaluate(
            _playerBody.PlanarPosition,
            _serverPlanarPosition,
            target,
            _npcMoveSent);
        if (decision.StopLocally)
        {
            StopPredictedMovement();
        }

        if (decision.SendStop)
        {
            _npcMoveSent = true;
            _reports.Request();
        }

        if (decision.OpenDialog)
        {
            var entityId = _npcTargetId;
            ClearAttackTarget();
            switch (_approach)
            {
                case ApproachKind.Npc:
                    NpcSelected?.Invoke(entityId);
                    break;
                case ApproachKind.Seller when _remote.ShopSigns.ContainsKey(entityId):
                    ShopSellerSelected?.Invoke(entityId);
                    break;
                case ApproachKind.Player:
                    ReachedPlayerId = entityId;
                    break;
            }

            return;
        }

        if (decision.BeginApproach)
        {
            _moveTarget = decision.Destination;
            _hasMoveTarget = true;
            _npcMoveSent = true;
            _reports.Request();
        }
    }

    private void UpdateAttackPresentation(GameTime gameTime)
    {
        if (_attackInput.TargetId == 0 || _remote == null)
        {
            return;
        }

        // A skill motion owns the avatar: a target picked during it is kept, and the approach and swing start once it ends.
        if (_playerController.LocomotionFrozen || _selfAttacking || SelfStunned || SelfCasting)
        {
            return;
        }

        if (!_remote.TryGetTarget(_attackInput.TargetId, out var targetPos, out var targetDead) || targetDead)
        {
            ClearAttackTarget();
            if (_playerController.Current == MotionCode.Attack)
            {
                ApplyPlayerCommand(_playerController.ApplyLocomotion(MotionCode.Stand));
            }

            return;
        }

        var current = _playerBody.PlanarPosition;
        var targetPlanar = new Vector2(targetPos.X, targetPos.Z);
        var decision = AttackPresentation.Decide(current, targetPlanar, Owner?.AttackRange ?? AttackPresentation.MeleeRange);
        if (decision.ShouldAttack)
        {
            FaceToward(targetPlanar);
            ApplyPlayerCommand(_playerController.ApplyLocomotion(MotionCode.Stand));

            return;
        }

        var elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var dir = decision.ApproachDirection;
        var speed = MovementSpeed(_runMode);
        var step = speed * elapsed;
        var probeY = _playerBody.Height + BodyProbeHeight;
        var slid = MoveWithCollision(current, dir * step, probeY);
        CommitGroundMovement(ResolveEntityCollision(current, slid));
        _playerForward = new Vector3(dir.X, 0f, dir.Y);
        ApplyPlayerWorld();
        ApplyPlayerCommand(_playerController.ApplyLocomotion(_runMode ? MotionCode.Run : MotionCode.Walk));
    }
}
