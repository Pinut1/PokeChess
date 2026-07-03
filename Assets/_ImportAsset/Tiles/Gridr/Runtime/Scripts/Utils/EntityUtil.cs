//Created by Carl Hinas | https://www.generalgames.org
//generalgames.bsky.social


using System.Collections.Generic;
using System.Linq;
using Gridr.Adw;
using Gridr.Gameplay;
using UnityEngine;

namespace Gridr.Utils
{
    public static class EntityUtil
    {
        public static IEnumerable<GridEntity> GetEntities(Player player)
        {
            return player.GetEntities();
        }

        public static IEnumerable<GridEntity> GetActiveEntities(Player player)
        {
            return player.GetEntities().Where(e => e.Active);
        }

        public static IEnumerable<GridEntity> GetEntitiesOfSameTeam(Player player)
        {
            foreach (var gridEntity in player.GetEntities())
            {
                if (PropertyUtil.IsSameTeam(player, gridEntity))
                    yield return gridEntity;
            }
        }



        public static IEnumerable<GridEntity> GetEntitiesOfSameTeamWithCaptureAction(Player player)
        {
            foreach (var gridEntity in GetEntitiesOfSameTeam(player))
            {
                if (ActionUtil.GetAction<CaptureAction>(gridEntity) != null)
                    yield return gridEntity;
            }
        }

        public static int GetEntitiesOfSameTeamWithCaptureActionAmount(Player player)
        {
            var count = 0;

            foreach (var gridEntity in GetEntitiesOfSameTeam(player))
            {
                if (ActionUtil.GetAction<CaptureAction>(gridEntity) != null)
                    count++;
            }

            return count;
        }

        public static IEnumerable<GridEntity> GetEntitiesOfDifferentTeam(Player player)
        {
            foreach (var gridEntity in player.GetEntities())
            {
                if (!PropertyUtil.IsSameTeam(player, gridEntity))
                    yield return gridEntity;
            }
        }

        public static IEnumerable<GridEntity> GetCaptureTargetEntitiesOfDifferentTeam(Player player)
        {
            foreach (var gridEntity in GetEntitiesOfDifferentTeam(player))
            {
                if (PropertyUtil.GetProperty<CaptureTargetProperty>(gridEntity) != null)
                    yield return gridEntity;
            }
        }
        

        public static int GetCaptureTargetEntitiesOfDifferentTeamAmount(Player player)
        {
            int count = 0;
            foreach (var gridEntity in GetCaptureTargetEntitiesOfDifferentTeam(player))
            {
                if (PropertyUtil.GetProperty<CaptureTargetProperty>(gridEntity) != null)
                    count++;
            }

            return count;
        }
        
        
        public static int GetCaptureTargetEntitiesOfSameTeamAmount(Player player)
        {
            int count = 0;
            foreach (var gridEntity in GetCaptureTargetEntitiesOfSameTeam(player))
            {
                if (PropertyUtil.GetProperty<CaptureTargetProperty>(gridEntity) != null)
                    count++;
            }

            return count;
        }

        public static IEnumerable<GridEntity> GetCaptureTargetEntitiesOfSameTeam(Player player)
        {
            foreach (var gridEntity in GetEntitiesOfSameTeam(player))
            {
                if (PropertyUtil.GetProperty<CaptureTargetProperty>(gridEntity) != null)
                    yield return gridEntity;
            }
        }
        
        public static IEnumerable<GridEntity> GetOthersEntitiesWithResource(Player player)
        {
            var entities = player.GetEntities().Where(e => PropertyUtil.GetProperty<GoldResourceProperty>(e) != null && !PropertyUtil.IsSameTeam(e, player));
            foreach (var entity in entities)
            {
                if(!PropertyUtil.HasProperty<GoldResourceProperty>(entity))
                    continue;
                yield return entity;
            }
        }
        
        public static IEnumerable<GridEntity> GetOthersEntitiesCaptureTarget(Player player)
        {
            var entities = player.GetEntities().Where(e => PropertyUtil.GetProperty<CaptureTargetProperty>(e) != null && !PropertyUtil.IsSameTeam(e, player));
            foreach (var entity in entities)
            {
                if(!PropertyUtil.HasProperty<CaptureTargetProperty>(entity))
                    continue;
                yield return entity;
            }
        }

        public static GridEntity GetClosestAttackTargetOfDifferentTeam(GridEntity entity, Player player)
        {
            var entities = player.GetEntities();
            var otherTeamEntities = entities.Where(target => PropertyUtil.HasPropertyOfType<IAttackTarget>(target) && !PropertyUtil.IsSameTeam(target, entity));

            return otherTeamEntities.OrderBy(e => Vector3.Distance(entity.transform.position, e.transform.position)).FirstOrDefault();
        }

        public static IEnumerable<GridEntity> GetEntitiesOfSameTeamBySelectionPriority(Player player)
        {
            return GetEntitiesOfSameTeam(player).OrderBy(e => e.GetPriority());
        }
        
        public static IEnumerable<GridEntity> GetClosestEntities(Player player, GridEntity referenceEntity)
        {
            return !GetEntitiesOfDifferentTeam(player).Any() ? null : GetEntitiesOfDifferentTeam(player).OrderBy(c => Vector3.Distance(referenceEntity.transform.position, c.transform.position));
        }

        public static GridEntity GetClosestEnemyEntity(Player player, GridEntity referenceEntity)
        {
            return GetEntitiesOfDifferentTeam(player).Where(entity => !PropertyUtil.IsSameTeam(entity, player)).OrderBy(e => Vector3.Distance(referenceEntity.transform.position, e.transform.position)).FirstOrDefault();
        }

        
    }
}