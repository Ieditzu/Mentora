plugins {
    id("java")
    id("org.springframework.boot") version "3.2.4"
}

group = "io.github.kawase"
version = "1.0-SNAPSHOT"

java {
    toolchain {
        languageVersion.set(JavaLanguageVersion.of(21))
    }
}

repositories {
    mavenCentral()
}

dependencies {
    implementation(platform("org.springframework.boot:spring-boot-dependencies:3.2.12"))

    implementation("org.springframework.boot:spring-boot-starter-data-jpa")
    implementation("org.springframework.boot:spring-boot-starter-validation")

    implementation("io.hypersistence:hypersistence-utils-hibernate-63:3.15.2")

    implementation("org.postgresql:postgresql:42.7.10")
    implementation("org.java-websocket:Java-WebSocket:1.6.0")

    compileOnly("org.projectlombok:lombok:1.18.44")
    annotationProcessor("org.projectlombok:lombok:1.18.44")
    testCompileOnly("org.projectlombok:lombok:1.18.44")
    testAnnotationProcessor("org.projectlombok:lombok:1.18.44")

    testImplementation("org.springframework.boot:spring-boot-starter-test")

    implementation("com.fasterxml.jackson.core:jackson-databind")
    implementation("org.json:json:20240303")
}

tasks.withType<Test> {
    useJUnitPlatform()
}