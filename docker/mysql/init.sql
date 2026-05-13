CREATE DATABASE IF NOT EXISTS `LinkedInAI`
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

CREATE USER IF NOT EXISTS 'linkedinai_app'@'%' IDENTIFIED BY 'linkedinai_pass';
GRANT ALL PRIVILEGES ON `LinkedInAI`.* TO 'linkedinai_app'@'%';
FLUSH PRIVILEGES;
