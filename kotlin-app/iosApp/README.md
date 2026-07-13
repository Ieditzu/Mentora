# Mentora iOS

This folder contains the native SwiftUI iOS shell and its direct Kotlin Multiplatform framework integration.

## Prerequisites

- macOS with Xcode 16 or newer
- XcodeGen (`brew install xcodegen`) to generate the local Xcode project
- JDK 21 and the Android/Kotlin toolchain used by the repository

## Run it

From `kotlin-app/iosApp`:

```bash
xcodegen generate
open MentoraIOS.xcodeproj
```

The Xcode target's pre-build script runs `:shared:embedAndSignAppleFrameworkForXcode`, then imports `MentoraShared` into the SwiftUI application.

## Current migration boundary

The SwiftUI shell and shared Kotlin state/localization/models are in place. It intentionally does not authenticate against the production socket yet: the existing Android transport and binary packet codec use JVM-only Java WebSocket, `ByteBuffer`, and JCA AES APIs. The next migration step is a byte-for-byte portable Kotlin codec plus iOS transport implementation, preserving all existing packet IDs and server compatibility.
