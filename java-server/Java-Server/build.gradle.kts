plugins {
    id("java")
    id("jacoco")
    id("org.springframework.boot") version "3.2.12"
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

    implementation("org.springframework.boot:spring-boot-starter-web")
    implementation("org.springframework.boot:spring-boot-starter-data-jpa")
    implementation("org.springframework.boot:spring-boot-starter-validation")
    implementation("org.springframework.security:spring-security-crypto")

    implementation("io.hypersistence:hypersistence-utils-hibernate-63:3.15.2")

    implementation("org.postgresql:postgresql:42.7.10")
    implementation("org.java-websocket:Java-WebSocket:1.6.0")

    compileOnly("org.projectlombok:lombok:1.18.44")
    annotationProcessor("org.projectlombok:lombok:1.18.44")
    testCompileOnly("org.projectlombok:lombok:1.18.44")
    testAnnotationProcessor("org.projectlombok:lombok:1.18.44")

    testImplementation("org.springframework.boot:spring-boot-starter-test")
    testImplementation("org.springframework.boot:spring-boot-testcontainers")
    testImplementation("org.testcontainers:junit-jupiter")
    testImplementation("org.testcontainers:postgresql")

    implementation("com.fasterxml.jackson.core:jackson-databind")
    implementation("org.json:json:20240303")
}

sourceSets.test {
    resources.srcDir("../../test-fixtures")
}

val integrationTestSourceSet = sourceSets.create("integrationTest") {
    compileClasspath += sourceSets.main.get().output + configurations.testRuntimeClasspath.get()
    runtimeClasspath += output + compileClasspath
    resources.srcDir("../../test-fixtures")
}

val dockerTestSourceSet = sourceSets.create("dockerTest") {
    compileClasspath += sourceSets.main.get().output + configurations.testRuntimeClasspath.get()
    runtimeClasspath += output + compileClasspath
    resources.srcDir("../../test-fixtures")
}

configurations[integrationTestSourceSet.implementationConfigurationName]
    .extendsFrom(configurations.testImplementation.get())
configurations[integrationTestSourceSet.runtimeOnlyConfigurationName]
    .extendsFrom(configurations.testRuntimeOnly.get())
configurations[dockerTestSourceSet.implementationConfigurationName]
    .extendsFrom(configurations.testImplementation.get())
configurations[dockerTestSourceSet.runtimeOnlyConfigurationName]
    .extendsFrom(configurations.testRuntimeOnly.get())

tasks.withType<Test> {
    useJUnitPlatform()
}

val integrationTest by tasks.registering(Test::class) {
    description = "Runs PostgreSQL-backed integration tests."
    group = LifecycleBasePlugin.VERIFICATION_GROUP
    testClassesDirs = integrationTestSourceSet.output.classesDirs
    classpath = integrationTestSourceSet.runtimeClasspath
    shouldRunAfter(tasks.test)
}

val dockerAdversarialTest by tasks.registering(Test::class) {
    description = "Runs adversarial tests against the disposable Docker code runners."
    group = LifecycleBasePlugin.VERIFICATION_GROUP
    testClassesDirs = dockerTestSourceSet.output.classesDirs
    classpath = dockerTestSourceSet.runtimeClasspath
    maxParallelForks = 1
    shouldRunAfter(integrationTest)
}

tasks.check {
    dependsOn(integrationTest)
}

tasks.jacocoTestReport {
    dependsOn(tasks.test, integrationTest)
    mustRunAfter(dockerAdversarialTest)
    executionData(fileTree(layout.buildDirectory).include("jacoco/*.exec"))
    reports {
        xml.required = true
        html.required = true
        csv.required = false
    }
}

tasks.jacocoTestCoverageVerification {
    dependsOn(tasks.test, integrationTest)
    mustRunAfter(dockerAdversarialTest)
    executionData(fileTree(layout.buildDirectory).include("jacoco/*.exec"))
    violationRules {
        rule {
            limit {
                minimum = "0.20".toBigDecimal()
            }
        }
    }
}
