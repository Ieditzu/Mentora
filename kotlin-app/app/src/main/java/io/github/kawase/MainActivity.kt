package io.github.kawase

import android.Manifest
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import androidx.lifecycle.viewmodel.compose.viewModel
import io.github.kawase.ui.AuthScreen
import io.github.kawase.ui.MainDashboard
import io.github.kawase.ui.SocketViewModel
import io.github.kawase.ui.theme.MentoraTheme
import kotlinx.coroutines.flow.collectLatest

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

    private fun requestNotificationPermission() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            if (ContextCompat.checkSelfPermission(this, Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED) {
                ActivityCompat.requestPermissions(this, arrayOf(Manifest.permission.POST_NOTIFICATIONS), 1001)
            }
        }
    }
}
