using UnityEngine;

namespace CK.SemesterProject.Tutorial
{
    public static class TutorialBattleCamera
    {
        private static readonly Collider[] _overlaps = new Collider[64];
        private static readonly RaycastHit[] _hits = new RaycastHit[64];

        // Offset: 플레이어 기준 오른쪽/높이/전방. 양수 X와 음수 Z가 어깨 뒤 구도다.
        public static bool TryGetPose(Vector3 playerSlot, Vector3 enemySlot, Transform player, Transform enemy,
            Vector3 offset, float fieldOfView, float aspect, out Pose pose)
        {
            pose = default;
            Vector3 forward = Vector3.ProjectOnPlane(enemySlot - playerSlot, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f || aspect <= 0 || fieldOfView <= 0 || fieldOfView >= 180)
            {
                return false;
            }
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            // 간격이 커져도 카메라가 같은 비율로 멀어져 거리감이 상쇄되지 않게 한다.
            float scale = Mathf.Sqrt(Mathf.Max(1f, Vector3.Distance(playerSlot, enemySlot) / 4f));
            Vector3 focus = Vector3.Lerp(playerSlot, enemySlot, 0.55f) + Vector3.up * 0.9f;
            for (int heightStep = 0; heightStep < 3; heightStep++)
            {
                float raise = heightStep * 0.4f;
                Vector3 position = playerSlot + (right * offset.x + forward * offset.z) * scale
                    + Vector3.up * (offset.y * scale + raise);
                Quaternion rotation = Quaternion.LookRotation(focus - position);
                if (!IsClear(position, player, enemy)
                    || !CanSee(position, playerSlot + Vector3.up * 0.25f, player, enemy)
                    || !CanSee(position, playerSlot + Vector3.up * 1.7f, player, enemy)
                    || !CanSee(position, enemySlot + Vector3.up * 0.2f, player, enemy)
                    || !CanSee(position, enemySlot + Vector3.up * 1.5f, player, enemy))
                {
                    continue;
                }
                Vector3 playerFeet = Project(playerSlot, position, rotation, fieldOfView, aspect);
                Vector3 playerHead = Project(playerSlot + Vector3.up * 1.7f, position, rotation, fieldOfView, aspect);
                Vector3 enemyCenter = Project(enemySlot + Vector3.up * 0.75f, position, rotation, fieldOfView, aspect);
                if (playerFeet.z <= 0 || playerHead.z <= 0 || enemyCenter.z <= 0
                    || playerFeet.x < 0.12f || playerFeet.x > 0.43f || playerFeet.y < 0.03f
                    || playerHead.y > 0.94f || enemyCenter.x < 0.5f || enemyCenter.x > 0.86f)
                {
                    continue;
                }
                pose = new Pose(position, rotation);
                return true;
            }
            return false;
        }

        public static bool TryGetOrbitPose(Pose basePose, Transform player, Transform enemy,
            float yaw, float pitch, float fieldOfView, float aspect, out Pose pose)
        {
            return TryGetOrbitPose(basePose, player.position, enemy.position, player, enemy,
                yaw, pitch, fieldOfView, aspect, out pose);
        }

        public static bool TryGetOrbitPose(Pose basePose, Vector3 playerSlot, Vector3 enemySlot,
            Transform player, Transform enemy, float yaw, float pitch, float fieldOfView, float aspect, out Pose pose)
        {
            Vector3 focus = Vector3.Lerp(playerSlot, enemySlot, 0.55f) + Vector3.up * 0.9f;
            Vector3 radial = Quaternion.AngleAxis(yaw, Vector3.up) * (basePose.position - focus);
            Vector3 right = Vector3.Cross(Vector3.up, -radial).normalized;
            Vector3 position = focus + Quaternion.AngleAxis(pitch, right) * radial;
            pose = new Pose(position, Quaternion.LookRotation(focus - position));
            return IsClear(position, player, enemy)
                && IsFramed(playerSlot + Vector3.up * 0.2f, pose, fieldOfView, aspect, player, enemy)
                && IsFramed(playerSlot + Vector3.up * 1.7f, pose, fieldOfView, aspect, player, enemy)
                && IsFramed(enemySlot + Vector3.up * 0.2f, pose, fieldOfView, aspect, player, enemy)
                && IsFramed(enemySlot + Vector3.up * 1.5f, pose, fieldOfView, aspect, player, enemy);
        }

        public static bool IsFramed(Vector3 point, Pose pose, float fieldOfView, float aspect,
            Transform player, Transform enemy)
        {
            Vector3 viewport = Project(point, pose.position, pose.rotation, fieldOfView, aspect);
            return viewport.z > 0f && viewport.x >= 0.04f && viewport.x <= 0.96f
                && viewport.y >= 0.03f && viewport.y <= 0.96f
                && CanSee(pose.position, point, player, enemy);
        }

        public static Vector3 GetMotionOffset(Vector3 offset, float time, float opening,
            float actionProgress, float actorDirection, float drift, float actionTravel)
        {
            float pulse = Mathf.Sin(Mathf.Clamp01(actionProgress) * Mathf.PI);
            offset.x += Mathf.Sin(time * 0.55f) * drift
                + actorDirection * Mathf.Sin(actionProgress * Mathf.PI * 2f) * actionTravel;
            offset.y += opening * 0.2f + Mathf.Sin(time * 0.4f) * drift * 0.25f;
            offset.z += pulse * actionTravel - opening * 0.65f;
            return offset;
        }

        public static bool CanTravel(Vector3 start, Vector3 end, Transform player, Transform enemy)
        {
            if (!IsClear(end, player, enemy))
            {
                return false;
            }
            Vector3 delta = end - start;
            if (delta.sqrMagnitude < 0.000001f)
            {
                return true;
            }
            int count = Physics.SphereCastNonAlloc(start, 0.2f, delta.normalized, _hits,
                delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (count == _hits.Length)
            {
                return false;
            }
            for (int i = 0; i < count; i++)
            {
                if (!IsParticipant(_hits[i].transform, player, enemy))
                {
                    return false;
                }
            }
            return true;
        }

        private static Vector3 Project(Vector3 point, Vector3 cameraPosition, Quaternion rotation, float fov, float aspect)
        {
            Vector3 local = Quaternion.Inverse(rotation) * (point - cameraPosition);
            float halfHeight = Mathf.Tan(fov * Mathf.Deg2Rad * 0.5f) * local.z;
            if (halfHeight <= 0)
            {
                return new Vector3(0, 0, -1);
            }
            return new Vector3(0.5f + local.x / (2 * halfHeight * aspect), 0.5f + local.y / (2 * halfHeight), local.z);
        }

        private static bool IsClear(Vector3 position, Transform player, Transform enemy)
        {
            int count = Physics.OverlapSphereNonAlloc(position, 0.2f, _overlaps, ~0, QueryTriggerInteraction.Ignore);
            if (count == _overlaps.Length)
            {
                return false;
            }
            for (int i = 0; i < count; i++)
            {
                if (!IsParticipant(_overlaps[i].transform, player, enemy))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool CanSee(Vector3 cameraPosition, Vector3 target, Transform player, Transform enemy)
        {
            Vector3 direction = target - cameraPosition;
            int count = Physics.RaycastNonAlloc(cameraPosition, direction.normalized, _hits, direction.magnitude,
                ~0, QueryTriggerInteraction.Ignore);
            if (count == _hits.Length)
            {
                return false;
            }
            for (int i = 0; i < count; i++)
            {
                if (!IsParticipant(_hits[i].transform, player, enemy))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsParticipant(Transform target, Transform player, Transform enemy)
        {
            return target.IsChildOf(player) || target.IsChildOf(enemy);
        }
    }
}
