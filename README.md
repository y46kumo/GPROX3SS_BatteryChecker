# G PRO X3 SUPERSTRIKE 배터리 체커

Logitech **G PRO X3 SUPERSTRIKE** 마우스의 배터리 잔량을 **작업 표시줄 오른쪽 아래(알림 영역)** 에 표시하는 Windows용 트레이 앱입니다.

## 기능

| 기능 | 설명 |
| --- | --- |
| 배터리 잔량 표시 | 트레이 아이콘이 **배터리 모양 게이지 + 퍼센트 숫자**로 표시됩니다. 잔량에 따라 색이 바뀝니다 (초록 > 50%, 노랑 ≤ 50%, 빨강 ≤ 20%, 파랑 = 충전 중). |
| 상태 창 | 트레이 아이콘을 **왼쪽 클릭**하면 화면 오른쪽 아래에 큰 배터리 게이지, 퍼센트, 충전 상태, 마지막 확인 시각이 뜹니다. |
| G HUB 열기 | 상태 창의 **G HUB 열기** 버튼이나 우클릭 메뉴로 Logitech G HUB를 실행합니다. 설치되어 있지 않으면 다운로드 페이지를 안내합니다. |
| 툴팁 | 아이콘에 마우스를 올리면 `배터리 85% (충전 중)` 같은 상태가 보입니다. |
| 마지막 값 유지 | 마우스가 절전 모드이거나 꺼져서 응답이 없으면, 마지막으로 읽은 잔량을 회색으로 표시합니다 (툴팁에 확인 시각 표시). |
| 배터리 부족 알림 | 방전 중 15%, 5%에 도달하면 Windows 알림을 띄웁니다. |
| 자동 실행 | 우클릭 메뉴 → **Windows 시작 시 자동 실행** |

갱신 주기: 연결되어 있을 때 30초, 장치를 찾지 못했을 때 15초마다 다시 찾습니다. 상태 창의 **새로고침** 버튼으로 바로 확인할 수도 있습니다.

## 동작 방식

G HUB와 같은 방식으로 Logitech **HID++ 2.0** 프로토콜을 사용해 마우스에 직접 배터리 상태를 물어봅니다.

- LIGHTSPEED 무선 수신기(슬롯 1~6)와 USB 유선 연결(충전 케이블)을 모두 찾습니다.
- 장치 이름에 `SUPERSTRIKE`가 들어간 장치를 우선 선택하고, 없으면 `PRO X` 계열, 그다음 배터리가 있는 다른 Logitech 장치를 선택합니다.
- 배터리 기능은 `UNIFIED_BATTERY (0x1004)` → `BATTERY_STATUS (0x1000)` → `BATTERY_VOLTAGE (0x1001)` 순서로 사용합니다. 전압 방식은 전압으로 잔량을 추정하며, 이 경우 상태 창에 "추정치"로 표시됩니다.
- HID 장치를 공유 모드로 열기 때문에 **G HUB가 실행 중이어도 함께 사용할 수 있고**, G HUB가 없어도 동작합니다.

## 설치 / 실행

### 1) 빌드된 exe 받기

GitHub Actions의 **build** 워크플로 결과물(`GProX3BatteryChecker-win-x64`)에서 `GProX3BatteryChecker.exe`를 받아 원하는 폴더(예: `C:\Tools\`)에 두고 실행하면 됩니다. .NET이 포함된 단일 실행 파일이라 별도 설치가 필요 없습니다.

### 2) 직접 빌드하기

[.NET 10 SDK](https://dotnet.microsoft.com/download)가 필요합니다.

```bash
dotnet publish src/GProX3BatteryChecker/GProX3BatteryChecker.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
```

`publish\GProX3BatteryChecker.exe`가 만들어집니다.

## 사용법

- **왼쪽 클릭**: 배터리 상태 창 열기/닫기 (바깥을 클릭하거나 `Esc`를 누르면 닫힘)
- **오른쪽 클릭**: 메뉴 (배터리 상태 보기 / 지금 새로고침 / G HUB 열기 / 자동 실행 / 종료)

### 아이콘이 작업 표시줄에 안 보일 때 (Windows 11)

Windows 11은 새 트레이 아이콘을 `^`(숨겨진 아이콘) 안에 넣습니다. 이 앱은 처음 실행될 때 자기 아이콘을 작업 표시줄에 항상 표시되도록 설정합니다. 사용자가 직접 바꾼 설정은 덮어쓰지 않습니다. 그래도 안 보이면:

**설정 → 개인 설정 → 작업 표시줄 → 기타 시스템 트레이 아이콘** 에서 `GProX3BatteryChecker`를 **켬**으로 바꾸세요.

## 문제 해결

- **회색으로 표시됨**: 마우스가 절전 상태라 응답이 없을 때입니다. 마우스를 움직이면 다음 갱신 때 다시 정상 표시됩니다.
- **"장치가 연결되지 않음"**: 마우스 전원이 켜져 있는지, 수신기가 꽂혀 있는지 확인하세요. 마우스가 절전 상태이면 한 번 움직인 뒤 **새로고침**을 누르세요.
- **다른 Logitech 장치가 표시됨**: 장치 이름에 `SUPERSTRIKE`가 들어간 장치를 찾지 못한 경우입니다. 마우스를 켠 뒤 새로고침하세요.
- 잔량 값은 마우스가 보고하는 값을 그대로 보여주므로 G HUB와 약간 다를 수 있습니다.

## 프로젝트 구조

```
src/GProX3BatteryChecker/
├── Program.cs                     진입점 (중복 실행 방지)
├── TrayApplicationContext.cs      트레이 아이콘, 메뉴, 주기적 갱신, 알림
├── BatteryStatus.cs               UI 상태 모델
├── Hidpp/                         HID++ 2.0 통신 및 배터리 읽기
│   ├── HidppChannel.cs
│   ├── HidppDevice.cs
│   ├── BatteryFeature.cs
│   └── LogitechBatteryService.cs
├── UI/
│   ├── BatteryPainter.cs          배터리 게이지 + 퍼센트 그리기 (공용)
│   ├── TrayIconRenderer.cs        트레이 아이콘 생성
│   ├── BatteryGauge.cs            상태 창의 큰 게이지
│   └── StatusPopup.cs             오른쪽 아래 상태 창
└── Services/
    ├── GHubLauncher.cs            G HUB 실행
    ├── AutoStart.cs               시작 프로그램 등록
    ├── TrayIconPromoter.cs        Windows 11 트레이 아이콘 항상 표시
    └── WindowsTheme.cs            작업 표시줄 밝은/어두운 테마 감지
```

의존성: [HidSharp](https://www.nuget.org/packages/HidSharp) (Apache-2.0)

> 이 프로젝트는 Logitech과 관련이 없는 비공식 도구입니다. Logitech, G HUB, LIGHTSPEED는 Logitech의 상표입니다.
