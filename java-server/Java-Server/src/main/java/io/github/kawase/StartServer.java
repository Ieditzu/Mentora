package io.github.kawase;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.context.ApplicationContext;

@SpringBootApplication
public class StartServer {
    public static void main(String[] args) {
        final ApplicationContext context = SpringApplication.run(StartServer.class, args);

        Server.getInstance().init(49154, context);
    }
}
