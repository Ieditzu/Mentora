package io.github.kawase

import android.Manifest
import android.content.pm.PackageManager
import android.content.res.Configuration
import android.os.Build
import android.os.Bundle
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.platform.LocalContext
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import androidx.lifecycle.viewmodel.compose.viewModel
import io.github.kawase.ui.AuthScreen
import io.github.kawase.ui.MainDashboard
import io.github.kawase.ui.SocketViewModel
import io.github.kawase.ui.theme.MentoraTheme
import kotlinx.coroutines.flow.collectLatest
import java.util.Locale

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        requestNotificationPermission()
        enableEdgeToEdge()
        setContent {
            val viewModel: SocketViewModel = viewModel()
            val isLoggedIn by viewModel.isLoggedIn
            val isDarkMode by viewModel.isDarkMode
            val primaryColor by viewModel.primaryColor
            val secondaryColor by viewModel.secondaryColor
            val appLanguage by viewModel.appLanguage
            val context = LocalContext.current
            val localizedContext = remember(appLanguage) {
                val configuration = Configuration(context.resources.configuration).apply {
                    setLocale(Locale.forLanguageTag(appLanguage))
                }
                context.createConfigurationContext(configuration)
            }

            CompositionLocalProvider(LocalContext provides localizedContext) {
                MentoraTheme(
                    darkTheme = isDarkMode,
                    primaryColor = primaryColor,
                    secondaryColor = secondaryColor
                ) {

                    if (isLoggedIn) {
                        MainDashboard(viewModel)
                    } else {
                        AuthScreen(viewModel)
                    }

                    LaunchedEffect(Unit) {
                        viewModel.connect()

                        viewModel.errorFlow.collectLatest { error ->
                            Toast.makeText(this@MainActivity, error, Toast.LENGTH_LONG).show()
                        }
                    }

                    LaunchedEffect(Unit) {
                        viewModel.successFlow.collectLatest { message ->
                            Toast.makeText(this@MainActivity, message, Toast.LENGTH_SHORT).show()
                        }
                    }
                }
            }
        }
    }

    private fun requestNotificationPermission() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            if (ContextCompat.checkSelfPermission(this, Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED) {
                ActivityCompat.requestPermissions(this, arrayOf(Manifest.permission.POST_NOTIFICATIONS), 1001)
            }
        }
    }
}
