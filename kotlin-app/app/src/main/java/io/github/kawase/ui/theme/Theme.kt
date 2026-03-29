package io.github.kawase.ui.theme

import android.app.Activity
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.SideEffect
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.platform.LocalView
import androidx.core.view.WindowCompat
import androidx.compose.ui.graphics.luminance

fun generateColorScheme(isDark: Boolean, primary: Color, secondary: Color): androidx.compose.material3.ColorScheme {
    return if (isDark) {
        darkColorScheme(
            primary = primary,
            onPrimary = if (primary.luminance() > 0.5f) Color.Black else Color.White,
            primaryContainer = primary.copy(alpha = 0.3f),
            onPrimaryContainer = Color.White,
            secondary = secondary,
            onSecondary = if (secondary.luminance() > 0.5f) Color.Black else Color.White,
            tertiary = GlassAccent,
            background = GlassBackgroundDark,
            surface = GlassSurfaceDark,
            onSurface = Color.White,
            onSurfaceVariant = Color.White.copy(alpha = 0.7f),
            outline = GlassBorderDark
        )
    } else {
        lightColorScheme(
            primary = primary,
            onPrimary = if (primary.luminance() > 0.5f) Color.Black else Color.White,
            primaryContainer = primary.copy(alpha = 0.15f),
            onPrimaryContainer = primary,
            secondary = secondary,
            onSecondary = if (secondary.luminance() > 0.5f) Color.Black else Color.White,
            tertiary = GlassAccent,
            background = GlassBackgroundLight,
            surface = GlassSurfaceLight,
            onSurface = TextTitle,
            onSurfaceVariant = TextBody,
            outline = GlassBorderLight
        )
    }
}

@Composable
fun MentoraTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    primaryColor: Color = GlassPrimary,
    secondaryColor: Color = GlassSecondary,
    content: @Composable () -> Unit
) {
    val colorScheme = generateColorScheme(darkTheme, primaryColor, secondaryColor)
    val view = LocalView.current
    if (!view.isInEditMode) {
        SideEffect {
            val window = (view.context as Activity).window
            window.statusBarColor = colorScheme.background.toArgb()
            WindowCompat.getInsetsController(window, view).isAppearanceLightStatusBars = !darkTheme
        }
    }

    MaterialTheme(
        colorScheme = colorScheme,
        typography = Typography,
        content = content
    )
}
