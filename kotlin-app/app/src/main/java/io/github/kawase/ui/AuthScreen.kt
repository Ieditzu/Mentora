package io.github.kawase.ui

import androidx.compose.animation.core.*
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Email
import androidx.compose.material.icons.filled.Lock
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.blur
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

import androidx.compose.foundation.Image
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.graphics.graphicsLayer
import io.github.kawase.R

@Composable
fun AuthScreen(viewModel: SocketViewModel) {
    var isLogin by remember { mutableStateOf(true) }
    var email by remember { mutableStateOf("") }
    var password by remember { mutableStateOf("") }
    val isConnected by viewModel.isConnected

    val infiniteTransition = rememberInfiniteTransition(label = "bg")
    val animOffset1 by infiniteTransition.animateFloat(
        initialValue = 0f, targetValue = 1000f,
        animationSpec = infiniteRepeatable(tween(20000, easing = LinearEasing), RepeatMode.Reverse), label = "1"
    )
    val animOffset2 by infiniteTransition.animateFloat(
        initialValue = 0f, targetValue = 1000f,
        animationSpec = infiniteRepeatable(tween(25000, easing = LinearEasing), RepeatMode.Reverse), label = "2"
    )
    val rotation by infiniteTransition.animateFloat(
        initialValue = 0f, targetValue = 360f,
        animationSpec = infiniteRepeatable(tween(40000, easing = LinearEasing), RepeatMode.Restart), label = "rot"
    )

    Box(modifier = Modifier.fillMaxSize().background(MaterialTheme.colorScheme.background)) {
        // Enhanced Dynamic Background
        Canvas(modifier = Modifier.fillMaxSize().blur(80.dp).graphicsLayer(rotationZ = rotation)) {
            drawCircle(
                color = viewModel.primaryColor.value.copy(alpha = 0.4f),
                radius = 600f,
                center = androidx.compose.ui.geometry.Offset(animOffset1 % size.width, animOffset2 % size.height)
            )
            drawCircle(
                color = viewModel.secondaryColor.value.copy(alpha = 0.3f),
                radius = 500f,
                center = androidx.compose.ui.geometry.Offset((size.width - animOffset2) % size.width, (size.height - animOffset1) % size.height)
            )
        }

        Column(
            modifier = Modifier.fillMaxSize().padding(24.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center
        ) {
            Image(
                painter = painterResource(id = R.drawable.logo),
                contentDescription = "Neuro Key Logo",
                modifier = Modifier
                    .size(150.dp)
                    .shadow(30.dp, RoundedCornerShape(40.dp))
                    .background(Color.White.copy(alpha = 0.05f), RoundedCornerShape(40.dp))
                    .border(1.dp, Color.White.copy(alpha = 0.15f), RoundedCornerShape(40.dp))
                    .padding(20.dp)
            )

            Spacer(modifier = Modifier.height(32.dp))

            Text(
                text = "Neuro Key",
                style = MaterialTheme.typography.displaySmall,
                color = MaterialTheme.colorScheme.onSurface,
                fontWeight = FontWeight.Black
            )
            Text(
                text = "Parental Monitoring Suite",
                style = MaterialTheme.typography.bodyLarge,
                color = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.6f),
                letterSpacing = 1.sp
            )

            Spacer(modifier = Modifier.height(48.dp))

            // Glassmorphic Container
            Surface(
                modifier = Modifier
                    .fillMaxWidth()
                    .border(1.dp, Color.White.copy(alpha = 0.2f), RoundedCornerShape(32.dp)),
                shape = RoundedCornerShape(32.dp),
                color = MaterialTheme.colorScheme.surface.copy(alpha = 0.4f),
                tonalElevation = 0.dp
            ) {
                Column(
                    modifier = Modifier.padding(32.dp),
                    horizontalAlignment = Alignment.CenterHorizontally
                ) {
                    Text(
                        text = if (isLogin) "Login" else "Create Account",
                        style = MaterialTheme.typography.titleLarge,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Spacer(modifier = Modifier.height(32.dp))

                    OutlinedTextField(
                        value = email,
                        onValueChange = { email = it },
                        label = { Text("Email Address") },
                        modifier = Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(16.dp),
                        leadingIcon = { Icon(Icons.Default.Email, contentDescription = null, tint = viewModel.primaryColor.value) },
                        colors = OutlinedTextFieldDefaults.colors(
                            focusedBorderColor = viewModel.primaryColor.value,
                            unfocusedBorderColor = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.1f),
                            focusedLabelColor = viewModel.primaryColor.value
                        ),
                        singleLine = true
                    )
                    Spacer(modifier = Modifier.height(20.dp))

                    OutlinedTextField(
                        value = password,
                        onValueChange = { password = it },
                        label = { Text("Password") },
                        visualTransformation = PasswordVisualTransformation(),
                        modifier = Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(16.dp),
                        leadingIcon = { Icon(Icons.Default.Lock, contentDescription = null, tint = viewModel.primaryColor.value) },
                        colors = OutlinedTextFieldDefaults.colors(
                            focusedBorderColor = viewModel.primaryColor.value,
                            unfocusedBorderColor = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.1f),
                            focusedLabelColor = viewModel.primaryColor.value
                        ),
                        singleLine = true
                    )

                    Spacer(modifier = Modifier.height(40.dp))

                    Button(
                        onClick = {
                            if (isLogin) viewModel.login(email, password)
                            else viewModel.register(email, password)
                        },
                        modifier = Modifier.fillMaxWidth().height(60.dp),
                        shape = RoundedCornerShape(20.dp),
                        colors = ButtonDefaults.buttonColors(
                            containerColor = viewModel.primaryColor.value
                        ),
                        elevation = ButtonDefaults.elevatedButtonElevation(defaultElevation = 8.dp)
                    ) {
                        Text(
                            if (isLogin) "Login" else "Register",
                            style = MaterialTheme.typography.titleMedium,
                            fontWeight = FontWeight.ExtraBold,
                            color = Color.White
                        )
                    }

                    Spacer(modifier = Modifier.height(16.dp))

                    TextButton(onClick = { isLogin = !isLogin }) {
                        Text(
                            if (isLogin) "Don't have an account? Register" else "Already have an account? Login",
                            color = viewModel.primaryColor.value,
                            fontWeight = FontWeight.SemiBold
                        )
                    }
                }
            }

            if (!isConnected) {
                Spacer(modifier = Modifier.height(32.dp))
                LinearProgressIndicator(
                    modifier = Modifier.fillMaxWidth(0.5f).height(2.dp).clip(CircleShape),
                    color = viewModel.primaryColor.value,
                    trackColor = viewModel.primaryColor.value.copy(alpha = 0.1f)
                )
                Spacer(modifier = Modifier.height(8.dp))
                Text("Connecting...", color = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.5f), fontSize = 12.sp)
            }
        }
    }
}
