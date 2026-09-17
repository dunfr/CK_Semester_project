using System;
using UnityEditor;
using UnityEngine;

namespace CK.SemesterProject.Battle.Editor
{
    public static class EditorMenuVisibility
    {
        private static double _nextMenuCheck;

        [InitializeOnLoadMethod]
        private static void ScheduleMenuCleanup()
        {
            EditorApplication.update += HideJobsMenu;
        }

        private static void HideJobsMenu()
        {
            // Burst가 지연 등록하거나 메뉴를 다시 만드는 경우도 처리한다.
            if (EditorApplication.timeSinceStartup < _nextMenuCheck)
            {
                return;
            }
            _nextMenuCheck = EditorApplication.timeSinceStartup + 2;
            // 사용자 요청으로 메뉴 표시만 숨긴다. Burst 패키지와 컴파일 설정은 유지한다.
            Type menu = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Menu");
            System.Reflection.MethodInfo remove = menu?.GetMethod("RemoveMenuItem",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (remove == null)
            {
                EditorApplication.update -= HideJobsMenu;
                Debug.LogWarning("현재 Unity 버전에서는 Jobs 메뉴 숨기기를 지원하지 않습니다.");
                return;
            }
            System.Reflection.MethodInfo exists = menu.GetMethod("MenuItemExists",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (exists != null && (bool)exists.Invoke(null, new object[] { "Jobs/Burst/Enable Compilation" }))
            {
                remove.Invoke(null, new object[] { "Jobs" });
            }
        }

    }
}
