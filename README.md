# Geuneda Statechart

Unity 프로젝트에서 Statechart(계층적 상태 머신)를 사용할 수 있게 해주는 패키지입니다.

## 개요

Statechart의 주요 특징은 상태를 계층 구조로 구성할 수 있다는 것입니다. Statechart는 각 상태가 하위 상태(substate)라 불리는 자체 하위 상태 머신을 정의할 수 있는 상태 머신입니다. 이러한 하위 상태도 다시 하위 상태를 정의할 수 있습니다.

자세한 정보: https://statecharts.github.io/what-is-a-statechart.html

## 요구 사항

- Unity 2022.3 이상
- UniTask 패키지 (`com.cysharp.unitask`)

## 설치 방법

### Unity Package Manager를 통한 설치

1. Unity 에디터에서 `Window` > `Package Manager`를 엽니다.
2. 좌측 상단의 `+` 버튼을 클릭하고 `Add package from git URL...`을 선택합니다.
3. 다음 URL을 입력합니다:
   ```
   https://github.com/geuneda/geuneda-statechart.git
   ```
4. `Add` 버튼을 클릭합니다.

### manifest.json을 통한 설치

프로젝트의 `Packages/manifest.json` 파일에 다음을 추가합니다:

```json
{
  "dependencies": {
    "com.geuneda.statechart": "https://github.com/geuneda/geuneda-statechart.git"
  }
}
```

## 네임스페이스

```csharp
using Geuneda.Statechart;
```

## 라이선스

MIT License
