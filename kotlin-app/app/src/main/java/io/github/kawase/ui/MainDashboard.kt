package io.github.kawase.ui

import android.Manifest
import android.content.pm.PackageManager
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.net.Uri
import android.util.Base64
import android.util.Size
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.activity.result.launch
import androidx.camera.core.CameraSelector
import androidx.camera.core.ImageAnalysis
import androidx.camera.core.Preview
import androidx.camera.lifecycle.ProcessCameraProvider
import androidx.camera.view.PreviewView
import androidx.compose.animation.*
import androidx.compose.animation.core.*
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.*
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.blur
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.graphics.luminance
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalLifecycleOwner
import androidx.compose.ui.platform.LocalSoftwareKeyboardController
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.compose.ui.window.Dialog
import androidx.core.content.ContextCompat
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.navigation.NavController
import androidx.navigation.NavHostController
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import coil.compose.AsyncImage
import com.google.mlkit.vision.barcode.BarcodeScanning
import com.google.mlkit.vision.barcode.BarcodeScannerOptions
import com.google.mlkit.vision.barcode.common.Barcode
import com.google.mlkit.vision.common.InputImage
import java.io.ByteArrayOutputStream
import java.util.concurrent.Executors

sealed class Screen(val route: String, val icon: ImageVector, val label: String) {
    object Home : Screen("home", Icons.Default.Home, "Home")
    object History : Screen("history", Icons.AutoMirrored.Filled.List, "History")
    object Goals : Screen("goals", Icons.Default.Star, "Goals")
    object Settings : Screen("settings", Icons.Default.Settings, "Settings")
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MainDashboard(viewModel: SocketViewModel) {
    val navController = rememberNavController()
    var selectedChildId by remember { mutableStateOf(-1L) }
    val children = viewModel.children
    
    val navBackStackEntry by navController.currentBackStackEntryAsState()
    val currentRoute = navBackStackEntry?.destination?.route
    var showAddGoalDialog by remember { mutableStateOf(false) }
    var showQRDialog by remember { mutableStateOf<Child?>(null) }

    val infiniteTransition = rememberInfiniteTransition(label = "bg")
    val rotation by infiniteTransition.animateFloat(
        initialValue = 0f, targetValue = 360f,
        animationSpec = infiniteRepeatable(tween(50000, easing = LinearEasing), RepeatMode.Restart), label = "rot"
    )

    Box(modifier = Modifier.fillMaxSize().background(MaterialTheme.colorScheme.background)) {
        // Dynamic Glass Background
        Box(modifier = Modifier.fillMaxSize().blur(100.dp).graphicsLayer(rotationZ = rotation)) {
            Box(modifier = Modifier.offset(x = (-100).dp, y = (-100).dp).size(300.dp).background(viewModel.primaryColor.value.copy(alpha = 0.15f), CircleShape))
            Box(modifier = Modifier.align(Alignment.BottomEnd).offset(x = 100.dp, y = 100.dp).size(400.dp).background(viewModel.primaryColor.value.copy(alpha = 0.1f), CircleShape))
        }

        Scaffold(
            containerColor = Color.Transparent,
            topBar = {
                CenterAlignedTopAppBar(
                    title = { 
                        Text(
                            "NEURO KEY",
                            style = MaterialTheme.typography.titleMedium,
                            fontWeight = FontWeight.Black,
                            letterSpacing = 2.sp,
                            color = MaterialTheme.colorScheme.onSurface
                        )
                    },
                    actions = {
                        if (currentRoute == Screen.Home.route) {
                            IconButton(onClick = { viewModel.fetchChildren() }) {
                                Icon(Icons.Default.Refresh, contentDescription = "Refresh", tint = viewModel.primaryColor.value)
                            }
                        }
                        if (currentRoute == Screen.Goals.route && selectedChildId != -1L) {
                            IconButton(onClick = { showAddGoalDialog = true }) {
                                Icon(Icons.Default.AddCircle, contentDescription = "New Goal", tint = viewModel.primaryColor.value, modifier = Modifier.size(28.dp))
                            }
                        }
                    },
                    colors = TopAppBarDefaults.centerAlignedTopAppBarColors(
                        containerColor = Color.Transparent,
                        titleContentColor = MaterialTheme.colorScheme.onSurface
                    )
                )
            },
            bottomBar = {
                // Floating Glass Navigation
                Box(modifier = Modifier.fillMaxWidth().padding(horizontal = 24.dp, vertical = 20.dp)) {
                    Surface(
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(72.dp)
                            .border(1.dp, if(viewModel.isDarkMode.value) Color.White.copy(alpha = 0.15f) else Color.Black.copy(alpha = 0.05f), CircleShape),
                        shape = CircleShape,
                        color = MaterialTheme.colorScheme.surface.copy(alpha = 0.95f),
                        tonalElevation = 8.dp,
                        shadowElevation = 12.dp
                    ) {
                        Row(
                            modifier = Modifier.fillMaxSize().padding(horizontal = 8.dp),
                            horizontalArrangement = Arrangement.SpaceAround,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            val items = listOf(Screen.Home, Screen.History, Screen.Goals, Screen.Settings)
                            items.forEach { screen ->
                                val selected = currentRoute == screen.route
                                val animatedScale by animateFloatAsState(if (selected) 1.2f else 1f, label = "scale")
                                val animatedColor by animateColorAsState(if (selected) viewModel.primaryColor.value else MaterialTheme.colorScheme.onSurface.copy(alpha = 0.5f), label = "color")

                                IconButton(
                                    onClick = {
                                        if (screen == Screen.Home) {
                                            navController.navigate(screen.route) {
                                                popUpTo(navController.graph.startDestinationId) { inclusive = true }
                                                launchSingleTop = true
                                            }
                                        } else if (screen == Screen.History || screen == Screen.Goals) {
                                            if (selectedChildId != -1L) {
                                                if (screen == Screen.History) viewModel.fetchCompletedTasks(selectedChildId)
                                                else viewModel.fetchGoals(selectedChildId)
                                                navController.navigate(screen.route) {
                                                    launchSingleTop = true
                                                    restoreState = true
                                                }
                                            }
                                        } else {
                                            navController.navigate(screen.route) {
                                                launchSingleTop = true
                                                restoreState = true
                                            }
                                        }
                                    },
                                    modifier = Modifier.size(48.dp)
                                ) {
                                    Column(horizontalAlignment = Alignment.CenterHorizontally) {
                                        Icon(
                                            screen.icon, 
                                            contentDescription = screen.label,
                                            modifier = Modifier.size(24.dp).graphicsLayer(scaleX = animatedScale, scaleY = animatedScale),
                                            tint = animatedColor
                                        )
                                        if (selected) {
                                            Box(modifier = Modifier.size(4.dp).background(animatedColor, CircleShape))
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        ) { innerPadding ->
            NavHost(navController, startDestination = "home", modifier = Modifier.padding(innerPadding)) {
                composable("home") {
                    HomeScreen(viewModel, children, 
                        onChildSelected = { childId ->
                            selectedChildId = childId
                            viewModel.fetchGoals(childId)
                            navController.navigate("goals")
                        },
                        onLogIntoGame = { child ->
                            showQRDialog = child
                        }
                    )
                }
                composable("history") {
                    HistoryScreen(viewModel)
                }
                composable("goals") {
                    GoalsScreen(viewModel, selectedChildId)
                }
                composable("settings") {
                    SettingsScreen(viewModel)
                }
            }
        }
    }

    if (showAddGoalDialog) {
        AddGoalDialog(
            tasks = viewModel.tasks,
            isDarkMode = viewModel.isDarkMode.value,
            primaryColor = viewModel.primaryColor.value,
            onDismiss = { showAddGoalDialog = false },
            onConfirm = { title, reward, points, taskId ->
                viewModel.addGoal(selectedChildId, title, reward, points, taskId)
                showAddGoalDialog = false
            }
        )
    }

    showQRDialog?.let { child ->
        QRScannerSimulatorDialog(
            child = child,
            isDarkMode = viewModel.isDarkMode.value,
            primaryColor = viewModel.primaryColor.value,
            onDismiss = { showQRDialog = null },
            onConfirm = { token ->
                viewModel.claimQRLogin(token, child.id)
                showQRDialog = null
            }
        )
    }
}

@Composable
fun QRScannerSimulatorDialog(
    child: Child,
    isDarkMode: Boolean,
    primaryColor: Color,
    onDismiss: () -> Unit,
    onConfirm: (String) -> Unit
) {
    var hasCameraPermission by remember { mutableStateOf(false) }
    val context = LocalContext.current
    
    val launcher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.RequestPermission(),
        onResult = { granted -> hasCameraPermission = granted }
    )

    LaunchedEffect(Unit) {
        if (ContextCompat.checkSelfPermission(context, Manifest.permission.CAMERA) == PackageManager.PERMISSION_GRANTED) {
            hasCameraPermission = true
        } else {
            launcher.launch(Manifest.permission.CAMERA)
        }
    }

    Dialog(onDismissRequest = onDismiss) {
        Surface(
            modifier = Modifier
                .fillMaxWidth()
                .clip(RoundedCornerShape(32.dp))
                .border(1.dp, if(isDarkMode) Color.White.copy(alpha = 0.15f) else Color.Black.copy(alpha = 0.08f), RoundedCornerShape(32.dp)),
            color = if(isDarkMode) Color(0xFF1A1A1A) else MaterialTheme.colorScheme.surface,
            tonalElevation = 8.dp
        ) {
            Column(modifier = Modifier.padding(24.dp), verticalArrangement = Arrangement.spacedBy(16.dp)) {
                Text("Scan QR Code for ${child.name}", style = MaterialTheme.typography.headlineSmall, fontWeight = FontWeight.Black, color = MaterialTheme.colorScheme.onSurface)
                
                if (hasCameraPermission) {
                    Box(modifier = Modifier.fillMaxWidth().height(300.dp).clip(RoundedCornerShape(24.dp))) {
                        QRScannerView(onCodeScanned = { onConfirm(it) })
                    }
                } else {
                    Box(modifier = Modifier.fillMaxWidth().height(300.dp).background(Color.Black.copy(alpha = 0.1f), RoundedCornerShape(24.dp)), contentAlignment = Alignment.Center) {
                        Text("Camera permission required", color = MaterialTheme.colorScheme.onSurface)
                    }
                }

                var manualToken by remember { mutableStateOf("") }
                val keyboardController = LocalSoftwareKeyboardController.current

                OutlinedTextField(
                    value = manualToken,
                    onValueChange = { if (!it.contains("\n")) manualToken = it },
                    label = { Text("Or enter token manually") },
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(16.dp),
                    singleLine = true,
                    keyboardOptions = KeyboardOptions(
                        imeAction = ImeAction.Done
                    ),
                    keyboardActions = KeyboardActions(
                        onDone = {
                            keyboardController?.hide()
                            if (manualToken.isNotBlank()) onConfirm(manualToken)
                        }
                    ),
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedBorderColor = primaryColor,
                        focusedLabelColor = primaryColor,
                        unfocusedTextColor = MaterialTheme.colorScheme.onSurface,
                        focusedTextColor = MaterialTheme.colorScheme.onSurface
                    )
                )

                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.End) {
                    TextButton(onClick = onDismiss) { Text("Cancel") }
                    Button(
                        onClick = { 
                            keyboardController?.hide()
                            onConfirm(manualToken) 
                        },
                        enabled = manualToken.isNotBlank(),
                        shape = RoundedCornerShape(12.dp),
                        colors = ButtonDefaults.buttonColors(containerColor = primaryColor)
                    ) {
                        Text("Log In", fontWeight = FontWeight.Bold, color = Color.White)
                    }
                }
            }
        }
    }
}

@Composable
fun QRScannerView(onCodeScanned: (String) -> Unit) {
    val context = LocalContext.current
    val lifecycleOwner = LocalLifecycleOwner.current
    val cameraProviderFuture = remember { ProcessCameraProvider.getInstance(context) }
    var scanned by remember { mutableStateOf(false) }

    AndroidView(
        factory = { ctx ->
            val previewView = PreviewView(ctx)
            val executor = Executors.newSingleThreadExecutor()
            
            cameraProviderFuture.addListener({
                val cameraProvider = cameraProviderFuture.get()
                val preview = Preview.Builder().build().also {
                    it.setSurfaceProvider(previewView.surfaceProvider)
                }

                val scanner = BarcodeScanning.getClient(
                    BarcodeScannerOptions.Builder()
                        .setBarcodeFormats(Barcode.FORMAT_QR_CODE)
                        .build()
                )
                
                val imageAnalysis = ImageAnalysis.Builder()
                    .setBackpressureStrategy(ImageAnalysis.STRATEGY_KEEP_ONLY_LATEST)
                    .build()

                imageAnalysis.setAnalyzer(executor) { imageProxy ->
                    val mediaImage = imageProxy.image
                    if (mediaImage != null && !scanned) {
                        val image = InputImage.fromMediaImage(mediaImage, imageProxy.imageInfo.rotationDegrees)
                        scanner.process(image)
                            .addOnSuccessListener { barcodes ->
                                for (barcode in barcodes) {
                                    barcode.rawValue?.let { code ->
                                        if (!scanned) {
                                            scanned = true
                                            onCodeScanned(code)
                                        }
                                    }
                                }
                            }
                            .addOnCompleteListener { imageProxy.close() }
                    } else {
                        imageProxy.close()
                    }
                }

                val cameraSelector = CameraSelector.DEFAULT_BACK_CAMERA
                try {
                    cameraProvider.unbindAll()
                    cameraProvider.bindToLifecycle(lifecycleOwner, cameraSelector, preview, imageAnalysis)
                } catch (e: Exception) {
                    e.printStackTrace()
                }
            }, ContextCompat.getMainExecutor(ctx))
            previewView
        },
        modifier = Modifier.fillMaxSize()
    )
}

@Composable
fun HomeScreen(viewModel: SocketViewModel, children: List<Child>, onChildSelected: (Long) -> Unit, onLogIntoGame: (Child) -> Unit) {
    Column(modifier = Modifier.fillMaxSize().padding(24.dp)) {
        Text(
            "My Kids",
            style = MaterialTheme.typography.headlineMedium,
            fontWeight = FontWeight.ExtraBold,
            color = MaterialTheme.colorScheme.onSurface
        )
        Text(
            "Monitor your children's progress",
            style = MaterialTheme.typography.bodyMedium,
            color = if (viewModel.isDarkMode.value) Color.White.copy(alpha = 0.8f) else Color.Black.copy(alpha = 0.6f)
        )
        
        Spacer(modifier = Modifier.height(32.dp))
        
        if (children.isEmpty()) {
            Box(modifier = Modifier.weight(1f).fillMaxWidth(), contentAlignment = Alignment.Center) {
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    Icon(Icons.Default.Face, contentDescription = null, modifier = Modifier.size(80.dp), tint = viewModel.primaryColor.value.copy(alpha = 0.3f))
                    Spacer(modifier = Modifier.height(24.dp))
                    Text("No kids added yet", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.onSurface)
                    TextButton(onClick = { /* navigate to settings */ }) {
                        Text("Add a kid in Settings", color = viewModel.primaryColor.value, fontWeight = FontWeight.Bold)
                    }
                }
            }
        } else {
            LazyColumn(
                verticalArrangement = Arrangement.spacedBy(20.dp),
                modifier = Modifier.weight(1f)
            ) {
                items(children) { child ->
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .clip(RoundedCornerShape(24.dp))
                            .background(MaterialTheme.colorScheme.surface.copy(alpha = 0.7f))
                            .border(1.dp, if (viewModel.isDarkMode.value) Color.White.copy(alpha = 0.15f) else Color.Black.copy(alpha = 0.08f), RoundedCornerShape(24.dp))
                            .clickable { onChildSelected(child.id) }
                            .padding(24.dp)
                    ) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            PfpView(child.pfp, child.name, viewModel.primaryColor.value, Modifier.size(60.dp), null)
                            
                            Spacer(modifier = Modifier.width(20.dp))
                            
                            Column(modifier = Modifier.weight(1f)) {
                                Row(verticalAlignment = Alignment.CenterVertically) {
                                    Text(
                                        child.name,
                                        style = MaterialTheme.typography.titleLarge,
                                        fontWeight = FontWeight.ExtraBold,
                                        color = MaterialTheme.colorScheme.onSurface
                                    )
                                    if (child.isOnline) {
                                        Spacer(modifier = Modifier.width(8.dp))
                                        Box(
                                            modifier = Modifier
                                                .size(8.dp)
                                                .background(Color(0xFF10B981), CircleShape)
                                                .shadow(4.dp, CircleShape)
                                        )
                                    }
                                }
                                Spacer(modifier = Modifier.height(4.dp))
                                Row(verticalAlignment = Alignment.CenterVertically) {
                                    Icon(Icons.Default.Star, contentDescription = null, modifier = Modifier.size(16.dp), tint = viewModel.primaryColor.value)
                                    Spacer(modifier = Modifier.width(4.dp))
                                    Text(
                                        "${child.points} Points",
                                        style = MaterialTheme.typography.bodyMedium,
                                        color = if (viewModel.isDarkMode.value) Color.White.copy(alpha = 0.8f) else Color.Black.copy(alpha = 0.6f)
                                    )
                                }
                            }

                            if (!child.isOnline) {
                                IconButton(
                                    onClick = { onLogIntoGame(child) },
                                    modifier = Modifier.size(44.dp).background(viewModel.primaryColor.value.copy(alpha = 0.1f), CircleShape)
                                ) {
                                    Icon(
                                        Icons.Default.QrCodeScanner,
                                        contentDescription = "Log into Game",
                                        tint = viewModel.primaryColor.value,
                                        modifier = Modifier.size(24.dp)
                                    )
                                }
                            } else {
                                IconButton(
                                    onClick = { onLogIntoGame(child) },
                                    modifier = Modifier.size(44.dp).background(MaterialTheme.colorScheme.error.copy(alpha = 0.1f), CircleShape)
                                ) {
                                    Icon(
                                        Icons.Default.Devices,
                                        contentDescription = "Force New Login",
                                        tint = MaterialTheme.colorScheme.error,
                                        modifier = Modifier.size(22.dp)
                                    )
                                }
                            }
                            
                            Spacer(modifier = Modifier.width(8.dp))
                            
                            Icon(
                                Icons.AutoMirrored.Filled.KeyboardArrowRight,
                                contentDescription = null,
                                tint = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.4f)
                            )
                        }
                    }
                }
                item { Spacer(modifier = Modifier.height(120.dp)) }
            }
        }
    }
}

@Composable
fun PfpView(base64: String?, name: String, primaryColor: Color, modifier: Modifier = Modifier, onEdit: (() -> Unit)? = null) {
    Box(modifier = modifier.clip(CircleShape).clickable(enabled = onEdit != null) { onEdit?.invoke() }, contentAlignment = Alignment.Center) {
        if (base64 != null && base64.isNotEmpty()) {
            val bitmap = remember(base64) {
                try {
                    val bytes = Base64.decode(base64, Base64.DEFAULT)
                    BitmapFactory.decodeByteArray(bytes, 0, bytes.size)
                } catch (e: Exception) { null }
            }
            if (bitmap != null) {
                Image(bitmap = bitmap.asImageBitmap(), contentDescription = null, contentScale = ContentScale.Crop, modifier = Modifier.fillMaxSize())
            } else {
                DefaultAvatar(name, primaryColor)
            }
        } else {
            DefaultAvatar(name, primaryColor)
        }
        
        if (onEdit != null) {
            Box(modifier = Modifier.fillMaxSize().background(Color.Black.copy(alpha = 0.3f)), contentAlignment = Alignment.Center) {
                Icon(Icons.Default.CameraAlt, contentDescription = null, tint = Color.White, modifier = Modifier.size(24.dp))
            }
        }
    }
}

@Composable
fun DefaultAvatar(name: String, primaryColor: Color) {
    Box(modifier = Modifier.fillMaxSize().background(primaryColor.copy(alpha = 0.15f)).border(2.dp, primaryColor, CircleShape), contentAlignment = Alignment.Center) {
        Text(name.take(1).uppercase(), style = MaterialTheme.typography.headlineSmall, fontWeight = FontWeight.Black, color = primaryColor)
    }
}

@Composable
fun ImagePickerBottomSheet(onImageSelected: (String) -> Unit, onDismiss: () -> Unit) {
    val context = LocalContext.current
    
    fun processBitmap(bitmap: Bitmap?) {
        if (bitmap == null) {
            android.widget.Toast.makeText(context, "Failed to capture or load image", android.widget.Toast.LENGTH_SHORT).show()
            return
        }
        try {
            val size = 200 // Max 200x200 for safe transport
            val scaled = if (bitmap.width > size || bitmap.height > size) {
                val ratio = bitmap.width.toFloat() / bitmap.height.toFloat()
                val w = if (ratio > 1) size else (size * ratio).toInt()
                val h = if (ratio > 1) (size / ratio).toInt() else size
                Bitmap.createScaledBitmap(bitmap, w, h, true)
            } else bitmap
            
            val outputStream = ByteArrayOutputStream()
            scaled.compress(Bitmap.CompressFormat.JPEG, 70, outputStream)
            val base64 = Base64.encodeToString(outputStream.toByteArray(), Base64.NO_WRAP)
            onImageSelected(base64)
        } catch (e: Exception) {
            e.printStackTrace()
            android.widget.Toast.makeText(context, "Error processing image", android.widget.Toast.LENGTH_SHORT).show()
        }
    }

    val galleryLauncher = rememberLauncherForActivityResult(ActivityResultContracts.GetContent()) { uri: Uri? ->
        uri?.let {
            try {
                val inputStream = context.contentResolver.openInputStream(it)
                val bitmap = BitmapFactory.decodeStream(inputStream)
                inputStream?.close()
                processBitmap(bitmap)
            } catch (e: Exception) {
                e.printStackTrace()
                android.widget.Toast.makeText(context, "Error loading from gallery", android.widget.Toast.LENGTH_SHORT).show()
            }
        }
        onDismiss()
    }
    
    val cameraLauncher = rememberLauncherForActivityResult(ActivityResultContracts.TakePicturePreview()) { bitmap: Bitmap? ->
        processBitmap(bitmap)
        onDismiss()
    }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Update Profile Picture") },
        text = { Text("Choose a source for your new profile picture.") },
        confirmButton = {
            TextButton(onClick = { 
                try {
                    galleryLauncher.launch("image/*")
                } catch (e: Exception) {
                    android.widget.Toast.makeText(context, "Could not open gallery", android.widget.Toast.LENGTH_SHORT).show()
                }
            }) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Default.PhotoLibrary, contentDescription = null)
                    Spacer(modifier = Modifier.width(8.dp))
                    Text("Gallery")
                }
            }
        },
        dismissButton = {
            TextButton(onClick = { 
                try {
                    cameraLauncher.launch()
                } catch (e: Exception) {
                    android.widget.Toast.makeText(context, "Could not open camera", android.widget.Toast.LENGTH_SHORT).show()
                }
            }) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Default.CameraAlt, contentDescription = null)
                    Spacer(modifier = Modifier.width(8.dp))
                    Text("Camera")
                }
            }
        }
    )
}

@Composable
fun ColorBox(color: Color, viewModel: SocketViewModel) {
    Box(
        modifier = Modifier
            .size(44.dp)
            .background(color, CircleShape)
            .border(
                if (viewModel.primaryColor.value == color) 3.dp else 0.dp,
                if (viewModel.primaryColor.value == color) MaterialTheme.colorScheme.onSurface else Color.Transparent,
                CircleShape
            )
            .clickable { viewModel.updatePrimaryColor(color) }
    )
}

@Composable
fun SettingsScreen(viewModel: SocketViewModel) {
    var childName by remember { mutableStateOf("") }
    val email by viewModel.email
    val scrollState = rememberScrollState()
    var showPfpPickerForId by remember { mutableStateOf<Long?>(null) } // -1 for parent, childId for kids

    Column(modifier = Modifier.fillMaxSize().padding(24.dp).verticalScroll(scrollState)) {
        Text("Settings", style = MaterialTheme.typography.headlineMedium, fontWeight = FontWeight.Black, color = MaterialTheme.colorScheme.onSurface)
        Spacer(modifier = Modifier.height(32.dp))
        
        Surface(
            modifier = Modifier.fillMaxWidth().border(1.dp, if (viewModel.isDarkMode.value) Color.White.copy(alpha = 0.15f) else Color.Black.copy(alpha = 0.08f), RoundedCornerShape(24.dp)),
            shape = RoundedCornerShape(24.dp),
            color = MaterialTheme.colorScheme.surface.copy(alpha = 0.7f)
        ) {
            Row(modifier = Modifier.padding(24.dp), verticalAlignment = Alignment.CenterVertically) {
                PfpView(viewModel.parentPfp.value, email, viewModel.primaryColor.value, Modifier.size(56.dp)) {
                    showPfpPickerForId = -1L
                }
                Spacer(modifier = Modifier.width(20.dp))
                Column {
                    Text("Parent Account", style = MaterialTheme.typography.labelMedium, color = viewModel.primaryColor.value, fontWeight = FontWeight.Bold)
                    Text(email, style = MaterialTheme.typography.bodyLarge, fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.onSurface)
                }
            }
        }

        Spacer(modifier = Modifier.height(32.dp))

        Text("App Theme", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.onSurface)
        Spacer(modifier = Modifier.height(16.dp))

        Surface(
            modifier = Modifier.fillMaxWidth().border(1.dp, if (viewModel.isDarkMode.value) Color.White.copy(alpha = 0.15f) else Color.Black.copy(alpha = 0.08f), RoundedCornerShape(24.dp)),
            shape = RoundedCornerShape(24.dp),
            color = MaterialTheme.colorScheme.surface.copy(alpha = 0.7f)
        ) {
            Column(modifier = Modifier.padding(24.dp)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text("Dark Mode", style = MaterialTheme.typography.titleSmall, modifier = Modifier.weight(1f), color = MaterialTheme.colorScheme.onSurface, fontWeight = FontWeight.Bold)
                    Switch(
                        checked = viewModel.isDarkMode.value,
                        onCheckedChange = { viewModel.toggleDarkMode() },
                        colors = SwitchDefaults.colors(
                            checkedThumbColor = Color.White,
                            checkedTrackColor = viewModel.primaryColor.value,
                            uncheckedThumbColor = Color.Gray,
                            uncheckedTrackColor = if(viewModel.isDarkMode.value) Color.DarkGray else Color.LightGray
                        )
                    )
                }
                Spacer(modifier = Modifier.height(24.dp))
                
                Text("Theme Color", style = MaterialTheme.typography.labelMedium, color = if (viewModel.isDarkMode.value) Color.White.copy(alpha = 0.8f) else Color.Black.copy(alpha = 0.5f))
                Spacer(modifier = Modifier.height(12.dp))
                
                Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    val themeColors = listOf(
                        Color(0xFF6366F1), Color(0xFF8B5CF6), Color(0xFFEC4899), Color(0xFFEF4444),
                        Color(0xFFF59E0B), Color(0xFF84CC16), Color(0xFF10B981), Color(0xFF06B6D4)
                    )
                    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                        themeColors.take(4).forEach { color -> ColorBox(color, viewModel) }
                    }
                    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                        themeColors.drop(4).forEach { color -> ColorBox(color, viewModel) }
                    }
                }
            }
        }

        Spacer(modifier = Modifier.height(32.dp))
        
        Text("Manage Kids", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.onSurface)
        Spacer(modifier = Modifier.height(16.dp))
        
        viewModel.children.forEach { child ->
            Surface(
                modifier = Modifier.fillMaxWidth().padding(bottom = 12.dp).border(1.dp, if (viewModel.isDarkMode.value) Color.White.copy(alpha = 0.1f) else Color.Black.copy(alpha = 0.05f), RoundedCornerShape(20.dp)),
                shape = RoundedCornerShape(20.dp),
                color = MaterialTheme.colorScheme.surface.copy(alpha = 0.7f)
            ) {
                Row(modifier = Modifier.padding(16.dp), verticalAlignment = Alignment.CenterVertically) {
                    PfpView(child.pfp, child.name, viewModel.primaryColor.value, Modifier.size(48.dp)) {
                        showPfpPickerForId = child.id
                    }
                    Spacer(modifier = Modifier.width(16.dp))
                    Text(child.name, style = MaterialTheme.typography.bodyLarge, fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.onSurface, modifier = Modifier.weight(1f))
                    
                    IconButton(onClick = { viewModel.removeChild(child.id) }) {
                        Icon(Icons.Default.Delete, contentDescription = "Remove Child", tint = MaterialTheme.colorScheme.error.copy(alpha = 0.7f))
                    }
                }
            }
        }

        Surface(
            modifier = Modifier.fillMaxWidth().border(1.dp, if (viewModel.isDarkMode.value) Color.White.copy(alpha = 0.15f) else Color.Black.copy(alpha = 0.08f), RoundedCornerShape(24.dp)),
            shape = RoundedCornerShape(24.dp),
            color = MaterialTheme.colorScheme.surface.copy(alpha = 0.7f)
        ) {
            Column(modifier = Modifier.padding(24.dp)) {
                val keyboardController = LocalSoftwareKeyboardController.current
                OutlinedTextField(
                    value = childName,
                    onValueChange = { if (!it.contains("\n")) childName = it },
                    label = { Text("Kid's Name") },
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(16.dp),
                    singleLine = true,
                    keyboardOptions = KeyboardOptions(imeAction = ImeAction.Done),
                    keyboardActions = KeyboardActions(onDone = { keyboardController?.hide() }),
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedBorderColor = viewModel.primaryColor.value,
                        focusedLabelColor = viewModel.primaryColor.value,
                        unfocusedTextColor = MaterialTheme.colorScheme.onSurface,
                        focusedTextColor = MaterialTheme.colorScheme.onSurface
                    )
                )
                Spacer(modifier = Modifier.height(20.dp))
                Button(
                    onClick = { 
                        if (childName.isNotBlank()) {
                            keyboardController?.hide()
                            viewModel.addChild(childName)
                            childName = ""
                        }
                    },
                    modifier = Modifier.fillMaxWidth().height(52.dp),
                    shape = RoundedCornerShape(16.dp),
                    colors = ButtonDefaults.buttonColors(containerColor = viewModel.primaryColor.value)
                ) {
                    Text("Add Kid", fontWeight = FontWeight.Bold, color = Color.White)
                }
            }
        }
        
        Spacer(modifier = Modifier.height(32.dp))

        Text("Developer Options", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.error)
        Spacer(modifier = Modifier.height(16.dp))
        
        var devChildId by remember { mutableStateOf("") }
        var devToken by remember { mutableStateOf("") }
        
        Surface(
            modifier = Modifier.fillMaxWidth().border(1.dp, MaterialTheme.colorScheme.error.copy(alpha = 0.3f), RoundedCornerShape(24.dp)),
            shape = RoundedCornerShape(24.dp),
            color = MaterialTheme.colorScheme.surface.copy(alpha = 0.7f)
        ) {
            Column(modifier = Modifier.padding(24.dp)) {
                OutlinedTextField(
                    value = devChildId,
                    onValueChange = { devChildId = it },
                    label = { Text("Manual Child ID") },
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(16.dp),
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedBorderColor = MaterialTheme.colorScheme.error,
                        focusedLabelColor = MaterialTheme.colorScheme.error,
                        unfocusedTextColor = MaterialTheme.colorScheme.onSurface,
                        focusedTextColor = MaterialTheme.colorScheme.onSurface
                    )
                )
                Spacer(modifier = Modifier.height(12.dp))
                OutlinedTextField(
                    value = devToken,
                    onValueChange = { devToken = it },
                    label = { Text("Manual Token") },
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(16.dp),
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedBorderColor = MaterialTheme.colorScheme.error,
                        focusedLabelColor = MaterialTheme.colorScheme.error,
                        unfocusedTextColor = MaterialTheme.colorScheme.onSurface,
                        focusedTextColor = MaterialTheme.colorScheme.onSurface
                    )
                )
                Spacer(modifier = Modifier.height(20.dp))
                Button(
                    onClick = { 
                        val id = devChildId.toLongOrNull()
                        if (id != null && devToken.isNotBlank()) {
                            viewModel.claimQRLogin(devToken, id)
                        }
                    },
                    modifier = Modifier.fillMaxWidth().height(52.dp),
                    shape = RoundedCornerShape(16.dp),
                    colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.error)
                ) {
                    Text("Force Game Login", fontWeight = FontWeight.Bold, color = Color.White)
                }
            }
        }

        Spacer(modifier = Modifier.height(32.dp))
        
        TextButton(
            onClick = { viewModel.logout() },
            modifier = Modifier.fillMaxWidth(),
            colors = ButtonDefaults.textButtonColors(contentColor = MaterialTheme.colorScheme.error)
        ) {
            Icon(Icons.AutoMirrored.Filled.ExitToApp, contentDescription = null)
            Spacer(modifier = Modifier.width(8.dp))
            Text("Logout", fontWeight = FontWeight.Bold)
        }
        
        Spacer(modifier = Modifier.height(100.dp))
    }

    showPfpPickerForId?.let { id ->
        ImagePickerBottomSheet(
            onImageSelected = { base64 -> viewModel.updatePfp(id, base64) },
            onDismiss = { showPfpPickerForId = null }
        )
    }
}

@Composable
fun HistoryScreen(viewModel: SocketViewModel) {
    val history = viewModel.completedTasks
    val isDark = viewModel.isDarkMode.value
    val subtextColor = if (isDark) Color.White.copy(alpha = 0.6f) else Color.Black.copy(alpha = 0.5f)

    // Group by date (extract date part from completedAt string)
    val grouped = remember(history) {
        history.groupBy { task ->
            task.completedAt.substringBefore(" ").substringBefore("T").ifBlank { task.completedAt }
        }.toSortedMap(compareByDescending { it })
    }
    val totalPoints = remember(history) { history.sumOf { it.pointValue } }

    Column(modifier = Modifier.fillMaxSize().padding(24.dp)) {
        Text("Task History", style = MaterialTheme.typography.headlineMedium, fontWeight = FontWeight.Black, color = MaterialTheme.colorScheme.onSurface)
        Text("Completed tasks from the game", style = MaterialTheme.typography.bodyMedium, color = subtextColor)

        if (history.isNotEmpty()) {
            Spacer(modifier = Modifier.height(16.dp))
            // Summary stats row
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                StatChip(label = "Total", value = "${history.size}", color = viewModel.primaryColor.value, isDark = isDark, modifier = Modifier.weight(1f))
                StatChip(label = "Points", value = "$totalPoints", color = Color(0xFF4CAF50), isDark = isDark, modifier = Modifier.weight(1f))
                StatChip(label = "Days", value = "${grouped.size}", color = Color(0xFFFF9800), isDark = isDark, modifier = Modifier.weight(1f))
            }
        }

        Spacer(modifier = Modifier.height(20.dp))

        if (history.isEmpty()) {
            Box(modifier = Modifier.weight(1f).fillMaxWidth(), contentAlignment = Alignment.Center) {
                Text("No tasks completed yet.", color = if (isDark) Color.White.copy(alpha = 0.4f) else Color.Black.copy(alpha = 0.3f))
            }
        } else {
            LazyColumn(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                grouped.forEach { (date, tasks) ->
                    val dayPoints = tasks.sumOf { it.pointValue }

                    item(key = "header_$date") {
                        Row(
                            modifier = Modifier.fillMaxWidth().padding(vertical = 6.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Text(
                                date,
                                style = MaterialTheme.typography.titleSmall,
                                fontWeight = FontWeight.Bold,
                                color = viewModel.primaryColor.value
                            )
                            Spacer(modifier = Modifier.width(8.dp))
                            Box(modifier = Modifier.weight(1f).height(1.dp).background(if (isDark) Color.White.copy(alpha = 0.1f) else Color.Black.copy(alpha = 0.06f)))
                            Spacer(modifier = Modifier.width(8.dp))
                            Text(
                                "${tasks.size} tasks  +$dayPoints pts",
                                style = MaterialTheme.typography.labelSmall,
                                color = subtextColor
                            )
                        }
                    }

                    items(tasks, key = { it.id }) { task ->
                        Surface(
                            modifier = Modifier.fillMaxWidth().border(1.dp, if (isDark) Color.White.copy(alpha = 0.15f) else Color.Black.copy(alpha = 0.08f), RoundedCornerShape(16.dp)),
                            shape = RoundedCornerShape(16.dp),
                            color = MaterialTheme.colorScheme.surface.copy(alpha = 0.7f)
                        ) {
                            Row(modifier = Modifier.padding(16.dp), verticalAlignment = Alignment.CenterVertically) {
                                Surface(modifier = Modifier.size(36.dp), shape = CircleShape, color = viewModel.primaryColor.value.copy(alpha = 0.15f)) {
                                    Box(contentAlignment = Alignment.Center) {
                                        Icon(Icons.Default.CheckCircle, contentDescription = null, modifier = Modifier.size(18.dp), tint = viewModel.primaryColor.value)
                                    }
                                }
                                Spacer(modifier = Modifier.width(12.dp))
                                Column(modifier = Modifier.weight(1f)) {
                                    Text(task.taskTitle, style = MaterialTheme.typography.bodyMedium, fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.onSurface)
                                    Row(verticalAlignment = Alignment.CenterVertically) {
                                        Text("${task.pointValue} pts", style = MaterialTheme.typography.bodySmall, color = viewModel.primaryColor.value, fontWeight = FontWeight.Black)
                                        Spacer(modifier = Modifier.width(8.dp))
                                        Text(task.completedAt.substringAfter(" ").substringAfter("T").take(5), style = MaterialTheme.typography.bodySmall, color = subtextColor)
                                    }
                                }
                            }
                        }
                    }
                }
                item { Spacer(modifier = Modifier.height(100.dp)) }
            }
        }
    }
}

@Composable
private fun StatChip(label: String, value: String, color: Color, isDark: Boolean, modifier: Modifier = Modifier) {
    Surface(
        modifier = modifier,
        shape = RoundedCornerShape(12.dp),
        color = color.copy(alpha = 0.1f)
    ) {
        Column(modifier = Modifier.padding(12.dp), horizontalAlignment = Alignment.CenterHorizontally) {
            Text(value, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Black, color = color)
            Text(label, style = MaterialTheme.typography.labelSmall, color = if (isDark) Color.White.copy(alpha = 0.6f) else Color.Black.copy(alpha = 0.5f))
        }
    }
}

@Composable
fun ProfileInsightCard(
    label: String,
    profile: AiProfile,
    accentColor: Color,
    isDarkMode: Boolean,
    onDetailClick: () -> Unit
) {
    var expanded by remember { mutableStateOf(false) }
    val subtextColor = if (isDarkMode) Color.White.copy(alpha = 0.7f) else Color.Black.copy(alpha = 0.55f)
    val total = profile.correctCount + profile.incorrectCount
    val accuracy = if (total == 0) 0f else profile.correctCount.toFloat() / total

    Surface(
        modifier = Modifier
            .fillMaxWidth()
            .border(1.dp, if (isDarkMode) Color.White.copy(alpha = 0.12f) else Color.Black.copy(alpha = 0.06f), RoundedCornerShape(16.dp))
            .clickable { expanded = !expanded },
        shape = RoundedCornerShape(16.dp),
        color = MaterialTheme.colorScheme.surface.copy(alpha = 0.7f)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Surface(
                    shape = RoundedCornerShape(8.dp),
                    color = accentColor.copy(alpha = 0.15f),
                    modifier = Modifier.padding(end = 10.dp)
                ) {
                    Text(
                        label,
                        modifier = Modifier.padding(horizontal = 10.dp, vertical = 4.dp),
                        style = MaterialTheme.typography.labelMedium,
                        fontWeight = FontWeight.Bold,
                        color = accentColor
                    )
                }
                Text(
                    profile.level.replaceFirstChar { it.uppercase() },
                    style = MaterialTheme.typography.labelSmall,
                    color = subtextColor
                )
                Spacer(modifier = Modifier.weight(1f))
                if (total > 0) {
                    Text(
                        "${String.format("%.0f", accuracy * 100)}%",
                        style = MaterialTheme.typography.titleSmall,
                        fontWeight = FontWeight.Bold,
                        color = when {
                            accuracy >= 0.8f -> Color(0xFF4CAF50)
                            accuracy >= 0.5f -> Color(0xFFFF9800)
                            else -> Color(0xFFF44336)
                        }
                    )
                }
            }

            Spacer(modifier = Modifier.height(6.dp))

            // Default: one-line summary
            Text(
                profile.summaryOneLine,
                style = MaterialTheme.typography.bodySmall,
                color = subtextColor,
                maxLines = if (expanded) Int.MAX_VALUE else 1
            )

            // Expanded: three-line summary
            AnimatedVisibility(visible = expanded) {
                Column {
                    Spacer(modifier = Modifier.height(8.dp))
                    Text(
                        profile.summaryThreeLine,
                        style = MaterialTheme.typography.bodySmall,
                        color = if (isDarkMode) Color.White.copy(alpha = 0.8f) else Color.Black.copy(alpha = 0.65f)
                    )
                    Spacer(modifier = Modifier.height(10.dp))

                    // Stats row
                    Row(horizontalArrangement = Arrangement.spacedBy(16.dp)) {
                        Column {
                            Text("${profile.correctCount}", fontWeight = FontWeight.Bold, style = MaterialTheme.typography.titleSmall, color = Color(0xFF4CAF50))
                            Text("Correct", style = MaterialTheme.typography.labelSmall, color = subtextColor)
                        }
                        Column {
                            Text("${profile.incorrectCount}", fontWeight = FontWeight.Bold, style = MaterialTheme.typography.titleSmall, color = Color(0xFFF44336))
                            Text("Wrong", style = MaterialTheme.typography.labelSmall, color = subtextColor)
                        }
                        Column {
                            Text("${profile.hintsUsed}", fontWeight = FontWeight.Bold, style = MaterialTheme.typography.titleSmall, color = Color(0xFFFF9800))
                            Text("Hints", style = MaterialTheme.typography.labelSmall, color = subtextColor)
                        }
                    }

                    Spacer(modifier = Modifier.height(10.dp))
                    Surface(
                        modifier = Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(10.dp),
                        color = accentColor.copy(alpha = 0.1f),
                        onClick = onDetailClick
                    ) {
                        Text(
                            "View full details",
                            modifier = Modifier.padding(vertical = 8.dp).fillMaxWidth(),
                            style = MaterialTheme.typography.labelMedium,
                            fontWeight = FontWeight.Bold,
                            color = accentColor,
                            textAlign = TextAlign.Center
                        )
                    }
                }
            }
        }
    }
}

@Composable
fun ProfileDetailDialog(
    label: String,
    profile: AiProfile,
    isDarkMode: Boolean,
    primaryColor: Color,
    onDismiss: () -> Unit
) {
    val subtextColor = if (isDarkMode) Color.White.copy(alpha = 0.75f) else Color.Black.copy(alpha = 0.6f)

    AlertDialog(
        onDismissRequest = onDismiss,
        confirmButton = {
            TextButton(onClick = onDismiss) {
                Text("Close")
            }
        },
        title = {
            Text("$label Profile", fontWeight = FontWeight.Bold)
        },
        text = {
            Column(modifier = Modifier.verticalScroll(rememberScrollState())) {
                // Summary
                Text(profile.summaryThreeLine, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurface)
                Spacer(modifier = Modifier.height(16.dp))

                // Stats
                ProfileDetailSection("Level", profile.level.replaceFirstChar { it.uppercase() }, subtextColor)
                ProfileDetailSection("Total Interactions", "${profile.totalInteractions}", subtextColor)
                ProfileDetailSection("Correct / Incorrect", "${profile.correctCount} / ${profile.incorrectCount}", subtextColor)
                ProfileDetailSection("Hints Used", "${profile.hintsUsed}", subtextColor)
                ProfileDetailSection("AI Chat Turns", "${profile.chatTurns}", subtextColor)
                Spacer(modifier = Modifier.height(12.dp))

                // Strengths
                ProfileDetailList("Strengths", profile.strengths, Color(0xFF4CAF50), subtextColor)
                ProfileDetailList("Needs Help With", profile.needsHelp, Color(0xFFF44336), subtextColor)
                ProfileDetailList("Struggle Concepts", profile.struggleConcepts, Color(0xFFFF9800), subtextColor)
                ProfileDetailList("Common Mistakes", profile.commonMistakes, Color(0xFFF44336), subtextColor)
                ProfileDetailList("Asked AI About", profile.helpTopics, primaryColor, subtextColor)
                ProfileDetailList("Recent Mistakes", profile.recentMistakes, Color(0xFFFF9800), subtextColor)
            }
        }
    )
}

@Composable
private fun ProfileDetailSection(label: String, value: String, subtextColor: Color) {
    Row(modifier = Modifier.fillMaxWidth().padding(vertical = 2.dp)) {
        Text("$label: ", style = MaterialTheme.typography.bodySmall, fontWeight = FontWeight.Bold, color = subtextColor)
        Text(value, style = MaterialTheme.typography.bodySmall, color = subtextColor)
    }
}

@Composable
private fun ProfileDetailList(label: String, items: List<String>, accentColor: Color, subtextColor: Color) {
    if (items.isEmpty()) return
    Spacer(modifier = Modifier.height(6.dp))
    Text(label, style = MaterialTheme.typography.labelMedium, fontWeight = FontWeight.Bold, color = accentColor)
    items.forEach { item ->
        Text("  - $item", style = MaterialTheme.typography.bodySmall, color = subtextColor)
    }
}

@Composable
fun GoalsScreen(viewModel: SocketViewModel, childId: Long) {
    val goals = viewModel.goals
    val cppProfile = viewModel.aiProfilesCpp[childId]
    val pythonProfile = viewModel.aiProfilesPython[childId]
    val generalProfile = viewModel.aiProfilesGeneral[childId]
    LaunchedEffect(childId) {
        if (childId != -1L) {
            viewModel.fetchChildProfile(childId)
        }
    }

    var detailPopupProfile by remember { mutableStateOf<Pair<String, AiProfile>?>(null) }

    val profiles = remember(cppProfile, pythonProfile, generalProfile) {
        listOf(
            Triple("C++", cppProfile, Color(0xFF2196F3)),
            Triple("Python", pythonProfile, Color(0xFF4CAF50)),
            Triple("General", generalProfile, Color(0xFFFF9800))
        ).filter { it.second != null }
    }

    LazyColumn(
        modifier = Modifier.fillMaxSize().padding(24.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp)
    ) {
        item {
            Text("Goals", style = MaterialTheme.typography.headlineMedium, fontWeight = FontWeight.Black, color = MaterialTheme.colorScheme.onSurface)
            Text("Set goals and rewards", style = MaterialTheme.typography.bodyMedium, color = if (viewModel.isDarkMode.value) Color.White.copy(alpha = 0.8f) else Color.Black.copy(alpha = 0.6f))
            Spacer(modifier = Modifier.height(24.dp))
        }

        if (childId != -1L) {
            item {
                Text("AI Insights", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.ExtraBold, color = MaterialTheme.colorScheme.onSurface)
                Spacer(modifier = Modifier.height(4.dp))
                if (profiles.isEmpty()) {
                    Text("Building profile from recent activity...", style = MaterialTheme.typography.bodyMedium, color = if (viewModel.isDarkMode.value) Color.White.copy(alpha = 0.7f) else Color.Black.copy(alpha = 0.5f))
                }
            }

            items(profiles, key = { it.first }) { (label, profile, accentColor) ->
                ProfileInsightCard(
                    label = label,
                    profile = profile!!,
                    accentColor = accentColor,
                    isDarkMode = viewModel.isDarkMode.value,
                    onDetailClick = { detailPopupProfile = label to profile }
                )
            }

            item { Spacer(modifier = Modifier.height(14.dp)) }
        }

        if (goals.isEmpty()) {
            item {
                Box(modifier = Modifier.fillParentMaxHeight(0.4f).fillMaxWidth(), contentAlignment = Alignment.Center) {
                    Text("No goals set yet.", color = if (viewModel.isDarkMode.value) Color.White.copy(alpha = 0.4f) else Color.Black.copy(alpha = 0.3f))
                }
            }
        } else {
            items(goals, key = { it.id }) { goal ->
                Surface(
                    modifier = Modifier.fillMaxWidth().border(1.dp, if (viewModel.isDarkMode.value) Color.White.copy(alpha = 0.15f) else Color.Black.copy(alpha = 0.08f), RoundedCornerShape(24.dp)),
                    shape = RoundedCornerShape(24.dp),
                    color = MaterialTheme.colorScheme.surface.copy(alpha = 0.7f)
                ) {
                    Row(modifier = Modifier.padding(24.dp), verticalAlignment = Alignment.CenterVertically) {
                        Column(modifier = Modifier.weight(1f)) {
                            Text(
                                goal.title,
                                style = MaterialTheme.typography.titleMedium,
                                fontWeight = FontWeight.ExtraBold,
                                color = if (goal.completed) viewModel.primaryColor.value else MaterialTheme.colorScheme.onSurface
                            )
                            Spacer(modifier = Modifier.height(4.dp))
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Icon(Icons.Default.ShoppingCart, contentDescription = null, modifier = Modifier.size(14.dp), tint = viewModel.primaryColor.value)
                                Spacer(modifier = Modifier.width(6.dp))
                                Text("Reward: ${goal.reward}", style = MaterialTheme.typography.bodySmall, color = if (viewModel.isDarkMode.value) Color.White.copy(alpha = 0.8f) else Color.Black.copy(alpha = 0.6f))
                            }
                        }

                        Surface(
                            shape = CircleShape,
                            color = if (goal.completed) viewModel.primaryColor.value else Color.Transparent,
                            border = if (goal.completed) null else androidx.compose.foundation.BorderStroke(2.dp, MaterialTheme.colorScheme.onSurface.copy(alpha = 0.1f))
                        ) {
                            Icon(
                                if (goal.completed) Icons.Default.Check else Icons.Default.Lock,
                                contentDescription = null,
                                modifier = Modifier.padding(8.dp).size(20.dp),
                                tint = if (goal.completed) Color.White else MaterialTheme.colorScheme.onSurface.copy(alpha = 0.3f)
                            )
                        }
                    }
                }
            }
        }

        item { Spacer(modifier = Modifier.height(100.dp)) }
    }

    if (detailPopupProfile != null) {
        val (label, profile) = detailPopupProfile!!
        ProfileDetailDialog(
            label = label,
            profile = profile,
            isDarkMode = viewModel.isDarkMode.value,
            primaryColor = viewModel.primaryColor.value,
            onDismiss = { detailPopupProfile = null }
        )
    }
}

@Composable
fun AddGoalDialog(
    tasks: List<Task>,
    isDarkMode: Boolean,
    primaryColor: Color,
    onDismiss: () -> Unit,
    onConfirm: (String, String, Int, Long) -> Unit
) {
    var title by remember { mutableStateOf("") }
    var reward by remember { mutableStateOf("") }
    var points by remember { mutableStateOf("50") }
    var selectedTaskId by remember { mutableStateOf(-1L) }
    var usePoints by remember { mutableStateOf(true) }
    val keyboardController = LocalSoftwareKeyboardController.current

    Dialog(onDismissRequest = onDismiss) {
        Surface(
            modifier = Modifier
                .fillMaxWidth()
                .clip(RoundedCornerShape(32.dp))
                .border(1.dp, if(isDarkMode) Color.White.copy(alpha = 0.15f) else Color.Black.copy(alpha = 0.08f), RoundedCornerShape(32.dp)),
            color = if(isDarkMode) Color(0xFF1A1A1A).copy(alpha = 0.95f) else MaterialTheme.colorScheme.surface.copy(alpha = 0.95f),
            tonalElevation = 8.dp
        ) {
            Column(
                modifier = Modifier.padding(32.dp),
                verticalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                Text(
                    "New Goal", 
                    style = MaterialTheme.typography.headlineSmall, 
                    fontWeight = FontWeight.Black, 
                    color = MaterialTheme.colorScheme.onSurface
                )
                
                Spacer(modifier = Modifier.height(8.dp))

                OutlinedTextField(
                    value = title,
                    onValueChange = { if (!it.contains("\n")) title = it },
                    label = { Text("Goal Name") },
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(16.dp),
                    singleLine = true,
                    keyboardOptions = KeyboardOptions(imeAction = ImeAction.Next),
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedBorderColor = primaryColor,
                        focusedLabelColor = primaryColor,
                        unfocusedTextColor = MaterialTheme.colorScheme.onSurface,
                        focusedTextColor = MaterialTheme.colorScheme.onSurface,
                        unfocusedContainerColor = if(isDarkMode) Color.Black.copy(alpha = 0.3f) else Color.Black.copy(alpha = 0.03f),
                        focusedContainerColor = if(isDarkMode) Color.Black.copy(alpha = 0.4f) else Color.Black.copy(alpha = 0.05f)
                    )
                )
                OutlinedTextField(
                    value = reward,
                    onValueChange = { if (!it.contains("\n")) reward = it },
                    label = { Text("Reward") },
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(16.dp),
                    singleLine = true,
                    keyboardOptions = KeyboardOptions(
                        imeAction = if (usePoints) ImeAction.Next else ImeAction.Done
                    ),
                    keyboardActions = KeyboardActions(
                        onDone = { keyboardController?.hide() }
                    ),
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedBorderColor = primaryColor,
                        focusedLabelColor = primaryColor,
                        unfocusedTextColor = MaterialTheme.colorScheme.onSurface,
                        focusedTextColor = MaterialTheme.colorScheme.onSurface,
                        unfocusedContainerColor = if(isDarkMode) Color.Black.copy(alpha = 0.3f) else Color.Black.copy(alpha = 0.03f),
                        focusedContainerColor = if(isDarkMode) Color.Black.copy(alpha = 0.4f) else Color.Black.copy(alpha = 0.05f)
                    )
                )
                
                HorizontalDivider(modifier = Modifier.padding(vertical = 8.dp), color = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.1f))
                
                Text("How to complete", style = MaterialTheme.typography.labelMedium, fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.onSurface)
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                    FilterChip(
                        selected = usePoints,
                        onClick = { usePoints = true },
                        label = { Text("Points") },
                        shape = RoundedCornerShape(12.dp),
                        modifier = Modifier.weight(1f)
                    )
                    FilterChip(
                        selected = !usePoints,
                        onClick = { usePoints = false },
                        label = { Text("Static Task") },
                        shape = RoundedCornerShape(12.dp),
                        modifier = Modifier.weight(1f)
                    )
                }

                if (usePoints) {
                    OutlinedTextField(
                        value = points,
                        onValueChange = { if (!it.contains("\n")) points = it },
                        label = { Text("Points Required") },
                        modifier = Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(16.dp),
                        singleLine = true,
                        keyboardOptions = KeyboardOptions(
                            imeAction = ImeAction.Done,
                            keyboardType = KeyboardType.Number
                        ),
                        keyboardActions = KeyboardActions(
                            onDone = { keyboardController?.hide() }
                        ),
                        colors = OutlinedTextFieldDefaults.colors(
                            focusedBorderColor = primaryColor,
                            focusedLabelColor = primaryColor,
                            unfocusedTextColor = MaterialTheme.colorScheme.onSurface,
                            focusedTextColor = MaterialTheme.colorScheme.onSurface,
                            unfocusedContainerColor = if(isDarkMode) Color.Black.copy(alpha = 0.3f) else Color.Black.copy(alpha = 0.03f),
                            focusedContainerColor = if(isDarkMode) Color.Black.copy(alpha = 0.4f) else Color.Black.copy(alpha = 0.05f)
                        )
                    )
                } else {
                    Text("Select Task:", style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.onSurface)
                    LazyColumn(modifier = Modifier.heightIn(max = 200.dp)) {
                        items(tasks) { task ->
                            Surface(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .clickable { selectedTaskId = task.id }
                                    .padding(vertical = 4.dp),
                                shape = RoundedCornerShape(12.dp),
                                color = if (selectedTaskId == task.id) primaryColor.copy(alpha = 0.2f) else Color.Transparent,
                                border = androidx.compose.foundation.BorderStroke(1.dp, if (selectedTaskId == task.id) primaryColor else MaterialTheme.colorScheme.onSurface.copy(alpha = 0.1f))
                            ) {
                                Row(modifier = Modifier.padding(16.dp), verticalAlignment = Alignment.CenterVertically) {
                                    RadioButton(selected = selectedTaskId == task.id, onClick = { selectedTaskId = task.id })
                                    Spacer(modifier = Modifier.width(12.dp))
                                    Text(task.name, style = MaterialTheme.typography.bodyMedium, fontWeight = if(selectedTaskId == task.id) FontWeight.Bold else FontWeight.Normal, color = MaterialTheme.colorScheme.onSurface)
                                }
                            }
                        }
                    }
                }

                Spacer(modifier = Modifier.height(16.dp))

                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.End) {
                    TextButton(onClick = onDismiss) {
                        Text("Cancel")
                    }
                    Spacer(modifier = Modifier.width(8.dp))
                    Button(
                        onClick = { 
                            keyboardController?.hide()
                            onConfirm(title, reward, points.toIntOrNull() ?: 0, if (usePoints) -1L else selectedTaskId) 
                        },
                        enabled = title.isNotBlank() && reward.isNotBlank(),
                        shape = RoundedCornerShape(12.dp),
                        colors = ButtonDefaults.buttonColors(containerColor = primaryColor)
                    ) {
                        Text("Save Goal", fontWeight = FontWeight.Bold, color = Color.White)
                    }
                }
            }
        }
    }
}
