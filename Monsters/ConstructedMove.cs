using BaseLib.Audio;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace BaseLib.Monsters;

/// <summary>
/// Helper class to construct a MoveState without defining a separate function,
/// while also handling most intents.
/// </summary>
public class MoveBuilder
{
    public readonly MonsterModel Monster;
    public readonly string Id;

    public readonly List<Func<IReadOnlyList<Creature>, Task>> Actions = [];
    public readonly List<AbstractIntent> Intents = [];
    
    
    public MoveBuilder(MonsterModel monster, string id)
    {
        Monster = monster;
        Id = id;
    }

    /// <summary>
    /// Adds an attack to the move.
    /// </summary>
    /// <param name="damage">Base damage of the attack.</param>
    /// <param name="hitCount">Number of hits.</param>
    /// <param name="attackerAnim">Name of animation attacker should play, duration of wait for animation, and whether animation should play on every hit.</param>
    /// <param name="attackerVfx"></param>
    /// <param name="attackerSfx"></param>
    /// <param name="attackerTmpSfx"></param>
    /// <param name="hitVfx"></param>
    /// <param name="hitSfx"></param>
    /// <param name="hitTmpSfx"></param>
    /// <returns></returns>
    public MoveBuilder Attack(int damage, int hitCount = 1, 
        (string, float, bool)? attackerAnim = null, string? attackerVfx = null, string? attackerSfx = null, string? attackerTmpSfx = null,
        string? hitVfx = null, string? hitSfx = null, string? hitTmpSfx = null)
    {
        Actions.Add(async _ =>
        {
            var cmd = MonsterActions.Attack(Monster, damage, hitCount)
                .WithAttackerFx(attackerVfx, attackerSfx, attackerTmpSfx)
                .WithHitFx(hitVfx, hitSfx, hitTmpSfx);

            if (attackerAnim.HasValue)
            {
                cmd.WithAttackerAnim(attackerAnim.Value.Item1, attackerAnim.Value.Item2);
                if (!attackerAnim.Value.Item3)
                {
                    cmd.OnlyPlayAnimOnce();
                }
            }

            await cmd.Execute(null);
        });

        if (hitCount != 1)
        {
            Intents.Add(new MultiAttackIntent(damage, hitCount));
        }
        else
        {
            Intents.Add(new SingleAttackIntent(damage));
        }
        
        return this;
    }

    /// <summary>
    /// Plays a sound using a specified key.
    /// </summary>
    public MoveBuilder PlaySfx(string key)
    {
        Actions.Add(_ =>
        {
            SfxCmd.Play(key);
            return Task.CompletedTask;
        });
        return this;
    }

    /// <summary>
    /// Plays a sound defined using ModSound.
    /// </summary>
    public MoveBuilder PlaySfx(ModSound sound, float volumeAdd = 0f, float volumeMult = 1f, float pitchVariation = 0f, float basePitch = 1f)
    {
        Actions.Add(_ =>
        {
            sound.Play(volumeAdd, volumeMult, pitchVariation, basePitch);
            return Task.CompletedTask;
        });
        return this;
    }

    /// <summary>
    /// Plays one of the creature's animations.
    /// </summary>
    public MoveBuilder PlayAnim(string animKey, float waitTime)
    {
        Actions.Add(async _ =>
        {
            await CreatureCmd.TriggerAnim(Monster.Creature, animKey, waitTime);
        });
        return this;
    }

    /// <summary>
    /// Adds a custom defined action to the action list.
    /// </summary>
    public MoveBuilder CustomAction(Func<IReadOnlyList<Creature>, Task> action)
    {
        Actions.Add(action);
        return this;
    }

    /// <summary>
    /// Adds an intent to the intent list.
    /// </summary>
    public MoveBuilder AddIntent(AbstractIntent intent)
    {
        Intents.Add(intent);
        return this;
    }

    /// <summary>
    /// Constructs MoveState from options supplied to builder.
    /// </summary>
    public MoveState Build()
    {
        return new MoveState(Id, new ActionExecutor(Actions), Intents.ToArray());
    }
    
    /// <summary>
    /// Implicit conversion to MoveState. Same result as calling <see cref="Build"/>.
    /// </summary>
    public static implicit operator MoveState(MoveBuilder builder)
    {
        return builder.Build();
    }

    private class ActionExecutor(List<Func<IReadOnlyList<Creature>, Task>> actions)
    {
        private List<Func<IReadOnlyList<Creature>, Task>> Actions { get; } = actions;

        public static implicit operator Func<IReadOnlyList<Creature>, Task>(ActionExecutor executor)
        {
            return async creatures =>
            {
                foreach (var action in executor.Actions)
                {
                    await action(creatures);
                }
            };
        }
    }
}