# Blender Python 제작 연결

무료 Blender를 Python으로 실행해 편집 가능한 `.blend` 원본과 렌더를 생성한다.
MCP 서버, 유료 애드온, 외부 AI API는 실행에 필요하지 않다. AI 서비스 자체의 사용료는 별개다.

## 실행

저장소 루트에서:

```powershell
python tools/blender/run.py
python tools/blender/run.py --script tools/blender/smoke_preview.py --output output/blender-smoke
python tools/blender/run.py --blender 'C:/path/to/blender.exe'
```

실행 파일 탐색 순서: `--blender` → `BLENDER_EXE` 환경변수 → PATH → 저장소
`tmp/blender/*/blender.exe` 및 표준 설치 경로의 후보 중 수정 시각이 최신인 파일.
Python 3.10 이상을 사용한다. Blender Python 작업에는 Blender에 포함된 `bpy`를 사용하므로
일반 Python에 `bpy`를 pip 설치하지 않는다.

사용자 스크립트는 Blender 안에서 실행되며 `-- --output <절대 경로>` 인자를 받는다.
실패하면 호출 명령도 실패하고, 자세한 출력은 지정 폴더의 UTF-8 `blender.log`에 남는다.
각 실행은 `--factory-startup`으로 독립된 초기 상태에서 시작한다.
이 명령은 열려 있는 Blender 화면을 실시간으로 조작하지 않는다.

## 설치된 로컬 버전

- Blender 4.5.9 LTS Windows x64 포터블.
- 실행 파일: `tmp/blender/blender-4.5.9-windows-x64/blender.exe`.
- 공식 ZIP: <https://download.blender.org/release/Blender4.5/blender-4.5.9-windows-x64.zip>
- 공식 체크섬: <https://download.blender.org/release/Blender4.5/blender-4.5.9.sha256>
- 검증된 ZIP SHA256: `41da973b9bf95bb312cbbeff4d1982feb13259b43c821686b9bafea4dfe5477cf`.
- `tmp/`는 Git 제외 경로다. 삭제하면 포터블도 사라지므로 다시 압축을 풀거나
  별도 설치 후 `--blender`로 지정한다. 다른 PC에는 자동 설치되지 않는다.

## 연결 확인 결과 (2026-11-23)

`python tools/blender/run.py` 종료 0, 로그의 `BLENDER_CONNECTION_OK` 확인.

- `output/blender-smoke/connection-sample.blend`: 링과 8개 독립 파편, 크기 키프레임, 정사영 카메라.
- `output/blender-smoke/preview.png`: 512×512 RGBA, 실제 알파 0~255 확인.
- `output/blender-smoke/manifest.json`: Blender 버전, 프레임, 레이어, 출력 규격.
- CPU Cycles 8 samples로 실행해 별도 GPU 설정 없이 렌더 확인.

연결 확인용 도형이며 최종 미술 시안이나 검수 승인으로 취급하지 않는다.
애니메이션은 키프레임 구성까지만 확인했고 전체 프레임 시퀀스 출력은 아직 수행하지 않았다.
게임 `Assets`로 자동 복사하거나 임포트하지 않는다.

## 실제 이펙트 제작

`docs/25-combat-effects-guide.md`에 따라 대표 형태 → 움직임 → 보조 효과 → Unity 연결 순서로 진행한다.
크기·방향·시간을 다르게 조정할 요소는 분리하고, Blender 미리보기와 Unity 최종 표시를 구분한다.
Blender 재질과 시뮬레이션이 Unity로 그대로 이식된다고 가정하지 않는다.
필요한 메시·텍스처·PNG 시퀀스를 출력하고 Unity 재질·입자·실제 이벤트 연결을 별도로 구성한다.
원본 `.blend`와 제작 스크립트는 `tmp/`가 아닌 작업별 보존 경로에 관리한 뒤 커밋 범위를 결정한다.
