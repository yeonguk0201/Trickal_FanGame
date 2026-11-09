using System;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Verify-0 (docs/30 §3.3): a Setup whose content later slices or tools now own. Re-running it would undo their
    // changes, so it stops before touching anything. Its Verify() still works; only the Setup and the batch entry
    // points that call it stop.
    public sealed class RetiredSetupException : InvalidOperationException
    {
        public RetiredSetupException(string message) : base(message)
        {
        }
    }

    public static class RetiredSetup
    {
        public const string Items =
            "아이템 에셋의 풀 소속·충전 값은 이후 조각(Pool-0·Power-2 등)이 바꿨습니다. 다시 실행하면 그 값이 되돌아갑니다.";
        public const string Rooms =
            "방·Encounter·장애물 배치는 Layout 가져오기(RoomLayoutImporter)와 이후 조각이 관리합니다. 다시 실행하면 방 Prefab이 예전 상태로 돌아갑니다.";
        public const string Enemies =
            "적·보스 수치와 등장 목록은 이후 조각(Curve-0, 적 추가 Setup 등)이 바꿨습니다. 다시 실행하면 그 값이 되돌아갑니다.";
        public const string GameHud =
            "Game HUD 배치는 이후 조각(UX-1 Week27Ux1Setup 등)이 관리합니다. 다시 실행하면 HUD 순서·문구·참조가 되돌아갑니다.";
        public const string Frontend =
            "Frontend 화면은 이후 조각(Setting·UX·Offline 등)이 바꿨습니다. 정식 디자인은 Menu 조각의 새 Setup으로 대체합니다.";

        public static void Stop(string setupName, string reason)
        {
            string message = $"{setupName}는 은퇴한 Setup입니다(Verify-0). {reason}";
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("은퇴한 Setup", message, "확인");
            throw new RetiredSetupException(message);
        }
    }
}
