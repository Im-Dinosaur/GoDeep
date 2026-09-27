# Fusion 온라인 개발 기록

갱신: 2026-09-28 · M2 첫 증분

## 목표와 구조

현재 로컬 조작 시제품을 유지하며 최대 4명이 같은 동굴에서 수영하는 첫 온라인 증분을 구현한다. 준비 화면에서 방을 만들거나 8자리 코드로 기존 방에 입장한다. 이 증분은 접속·이동·퇴장 흐름을 검증하는 단계다.

| 스크립트 | 책임 |
|---|---|
| `NetworkSession` | 외부 진입점. 연결 상태, 생성·입장·퇴장, 실패 후 메뉴 복귀 조율 |
| `FusionConnectionComponent` | SDK 설정 복사, 러너 수명, 방 연결, 동굴 로드와 로컬 스폰 |
| `NetworkDiverComponent` | 권한별 시점 구분, 입력 누적, Fusion 틱에서 수영, 머리 시선 동기화 |
| `NetworkMenuComponent` | 방 코드 입력, 중복 요청 차단, 연결 상태 표시 |
| 기존 `Player` | 온라인에서도 기존 이동·워치·표현 Component를 조합하는 플레이어 진입점 |
| 기존 `PrototypeSession` / `PrototypeUIComponent` | 로컬 연습과 온라인 이동 시험의 실행·표시 구분 |

호출 흐름은 `메뉴 → NetworkSession → FusionConnectionComponent → Fusion`이며, 플레이 중에는 `NetworkDiverComponent → Player → 이동·표현 Component`다. SDK가 요구하는 `Spawned`, `FixedUpdateNetwork`, `Render` 등의 이름은 그대로 사용한다.

## 현재 네트워크 결정

- Fusion **2.1.3 build 2390**, Shared 모드.
- 플레이어는 자신의 다이버에 상태 권한을 갖는다. 충돌 수영은 자기 권한에서 Fusion 틱마다 계산하고 `NetworkTransform`으로 위치·몸 방향을 전송한다. 머리 상하 시선은 별도 `[Networked]` 프로퍼티다.
- 원격 다이버는 입력·카메라·AudioListener·CharacterController를 사용하지 않는다. 임시 전신 도형으로 표시한다.
- 접속할 때마다 새 러너를 만들고, 종료한 러너를 재사용하지 않는다.
- `NetworkSceneManagerDefault`로 Build Settings의 기존 Play 씬을 동기화한다. 씬 로딩 완료 후 로컬 다이버를 한 번 생성해 PlayerObject로 등록한다.
- 방 최대 인원은 **4명**, 공개 목록 노출은 끈다. 코드를 아는 사람은 입장할 수 있으므로 비밀번호 인증방은 아니다.
- 8자리 영문·숫자 코드. 생성 시 새 코드를 부여하고, 입장은 `EnableClientSessionCreation=false`로 존재하는 방에만 허용한다.
- 초기 지역은 **asia**로 고정하고 호환 버전은 `godeep-m2-01`로 구분한다. 서로 다른 지역에 같은 코드의 별도 방이 생기는 문제를 방지한다. 최종 서비스 지역 선택 UI는 후속 작업이다.
- 저장된 App ID는 연결용 설정 복사본에서만 참조하며 원본을 변경하지 않는다. Git에는 App ID와 Photon SDK를 포함하지 않는다.

Shared 모드는 현재 협동 이동 시험의 구현 선택이다. 경쟁 게임 수준의 서버 권한이나 부정행위 방지를 제공한다고 가정하지 않는다. 산소·아이템·구조를 추가할 때는 대상의 상태 권한에서 처리하고 중복 소모와 동시 요청을 검증할 계획이다.

## 실행

1. Home → **PREPARE TO DIVE**로 준비 화면에 진입한다.
2. **CREATE ROOM**을 누르면 온라인 이동 시험이 시작된다.
3. 일시정지 안내에 표시되는 8자리 코드를 다른 참가자에게 알려준다.
4. 다른 참가자는 같은 버전·지역 설정의 실행 파일에서 코드를 입력하고 **JOIN ROOM**을 누른다.
5. **DIVE / RESUME**으로 수영을 시작한다. Esc는 자기 조작만 멈춘다.
6. 메뉴의 **LEAVE ROOM**을 누르면 방을 정리하고 준비 화면으로 돌아간다.

이번 온라인 증분에서는 산소를 고정하며 아이템 사용·보급·구조·도착 판정·워치 신호·음성은 아직 연결하지 않는다. UI에도 해당 범위를 표시한다. 기존 **START LOCAL PRACTICE**에서는 앞 단계의 산소·아이템·구조 시험이 그대로 작동한다.

## 검증 기록

- 기존 로컬 연습의 실제 플레이 모드 검사 **30개 통과**.
- 빈 코드·잘못된 코드·연결 중 중복 요청 차단.
- 실제 Photon Cloud 접속, Play 씬 로드, 자기 다이버 1개 생성, 로컬 AudioListener 1개 확인.
- 퇴장 시 러너와 PlayerObject 정리.
- 존재하지 않는 방 입장은 `GameNotFound`로 실패하고 방을 새로 만들지 않음.
- 접속 실패 후 같은 진입점에서 새 방 생성과 재퇴장 성공.
- Windows x64 개발 빌드 성공, 오류 0개. 첫 빌드에서 발견한 새 코드 경고 3개는 비동기 씬 이동 대기와 개발 검사 조건 수정으로 처리했다.
- **Unity 에디터 1개 + Windows 실행 파일 3개**가 실제 Photon Cloud의 동일 방에 접속했다. 각 프로세스가 자기 권한 1개와 원격 다이버 3개를 확인했다.
- Input System 검사 장치로 3개 클라이언트가 수영·회전했다. 각 클라이언트와 에디터가 같은 최종 위치·몸 회전·머리 상하 시선을 수신했다. 원격 카메라와 CharacterController는 비활성 상태였다.
- 별도의 5번째 참가자는 `GameIsFull`로 거절됐다.
- 방을 만든 에디터의 플레이 모드를 종료한 뒤에도 나머지 3개 클라이언트가 연결을 유지했다. 떠난 다이버는 각 프로세스에서 제거됐다.
- 온라인 전용 안내를 정리한 뒤 로컬 30개 검사, 실제 연결·스폰과 퇴장·실패 후 재접속을 다시 통과했다.

독립 실행 검사는 **같은 PC의 여러 프로세스**를 사용했다. 서로 다른 실제 PC, WAN 지연·패킷 손실, 마이크·스피커, 반복 장시간 플레이는 아직 검사하지 않았다. Shared 모드의 방장 이탈 유지가 전체 산소·아이템 상태 복구나 자동 재접속까지 구현됐다는 뜻은 아니다.

검증 결과는 `.utmp/network-20260928`의 `editor-4peers.json`, `before-owner-leave-smoke-*.json`, `after-owner-leave-smoke-*.json`, `connection-validation.json`, `reconnect-validation.json`, `final-build.json`에 둔다. 화면은 `online-4peers.png`와 안내 수정 후의 `online-movement.png`다. 실행 파일·로그·결과는 Git에 포함하지 않는다.

첫 빌드 경고 526개 중 510개는 설치된 패키지의 셰이더 관련 경고, 11개는 Photon SDK 경고, 3개는 수정한 프로젝트 코드 경고였다. 나머지 2개는 생성한 동굴 메시의 사전 충돌 베이크 안내와 Player에서 Pipeline 원격 실행을 사용하지 않는다는 안내다. SDK·패키지의 경고는 별도 호환성·빌드 최적화 작업으로 남긴다. 경고가 전혀 없다고 보고하지 않는다.

수정 후 최종 Windows 개발 빌드도 성공했다. **오류 0개, 프로젝트 작성 코드 경고 0개**이며, 이번 빌드 보고서에는 패키지 셰이더 510개·Photon 데모 2개·기타 안내 2개로 총 514개 경고가 남았다. 셰이더 경고에는 설치된 Unity AI Inference/Sentis 패키지의 셰이더 경고와 사용하지 않는 디버그 셰이더 제거 안내가 포함된다. 패키지 제거·교체는 이번 접속 구현 범위에서 수행하지 않았다.

최종 수정본 실행 파일로 독립 2인 연결·수영·회전 수신도 다시 확인했다(`final-2peers.json`). 최종 빌드의 크기는 약 229 MB이며 `.utmp/network-20260928/build/GoDeep.exe`로 직접 실행할 수 있다. 테스트용 Photon App ID가 포함되는 로컬 개발 산출물이므로 Git에 올리지 않는다.

검사 도구는 `Tools/GoDeepNetworkValidation.cs`다. `connect`는 플레이 모드에서 실제 임시 Photon 방을 만든다. `snapshot`은 연결 상태와 수신 위치를 읽고, `leaveAndRetry`는 퇴장·실패 복구를 검사한다. `buildWindows`는 플레이 모드 밖에서 `.utmp/network-20260928/build/GoDeep.exe`를 생성한다.

`NetworkSmokeClientComponent`는 개발 빌드에서 명시적으로 `-godeepSmokeRoom`과 `-godeepSmokeId`를 전달한 경우에만 동작한다. 실제 Input System 가상 장치로 이동하며 최대 150초 동안 수신 상태를 실행 파일 옆 `smoke-N.json`에 기록하고 퇴장한다. 일반 실행과 릴리즈 빌드에서는 자동 접속하지 않는다.

독립 클라이언트 실행 도구는 `Tools/Start-GoDeepNetworkSmoke.ps1`다. 외부 서버 검사이므로 실행 전에 같은 버전의 에디터 방을 만들고, 종료 후 검사 프로세스와 방 정리를 확인한다.

## 다음 증분

1. `NetworkDiverComponent`는 네트워크 상태와 요청 전달을 담당하고, 생존·소지품의 실제 처리는 `Player`와 기존 Component를 재사용한다.
2. 상태 권한이 있는 대상에서 산소 소모·행동불가·구조 상태를 확정한다. 산소팩 공유는 사거리·벽·대상 상태·재전송을 검증하고 효과와 소모가 한 번씩만 적용되게 한다.
3. 보급품의 동시 획득과 장치 사용을 동기화한다. 참가자마다 별도 사본을 소비하지 않도록 한다.
4. 워치 신호와 생존 상태를 UI에 전송한다. 음성은 이 증분에 포함하지 않고 다음 음성 작업에서 에어포켓 정책과 함께 연결한다.
5. 독립 2인부터 시작해 4인, 중복 사용·퇴장·늦은 입장 검사로 넓힌다. 검증되지 않은 동작은 현황 문서에 남긴다.

## 공식 참고

- [Shared 모드 이동과 카메라](https://doc.photonengine.com/fusion/v2/tutorials/shared-mode-basics/3-movement-and-camera)
- [방 연결과 매치메이킹](https://doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/matchmaking)
- [네트워크 씬 로딩](https://doc.photonengine.com/fusion/v2/manual/scene-loading)
- [VR Shared 샘플의 생성·퇴장 권한 설명](https://doc.photonengine.com/fusion/v2/technical-samples/fusion-vr-shared)
