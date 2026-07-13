import org.jetbrains.kotlin.gradle.dsl.JvmTarget

plugins {
    alias(libs.plugins.android.multiplatform.library)
    alias(libs.plugins.kotlin.multiplatform)
}

kotlin {
    androidLibrary {
        namespace = "io.github.kawase.shared"
        compileSdk = 36
        minSdk = 24
        compilerOptions {
            jvmTarget.set(JvmTarget.JVM_11)
        }
    }

    listOf(iosArm64(), iosSimulatorArm64(), iosX64()).forEach { iosTarget ->
        iosTarget.binaries.framework {
            baseName = "MentoraShared"
            isStatic = true
        }
        iosTarget.compilations.getByName("main").cinterops.create("CommonCrypto") {
            defFile(project.file("src/nativeInterop/cinterop/CommonCrypto.def"))
        }
    }

    sourceSets {
        commonMain.dependencies {
            implementation("org.jetbrains.kotlinx:kotlinx-coroutines-core:1.10.2")
        }
    }
}
