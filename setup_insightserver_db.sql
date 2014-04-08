-- MySQL dump 10.13  Distrib 5.5.35, for debian-linux-gnu (x86_64)
--
-- Host: localhost    Database: insight_stats
-- ------------------------------------------------------
-- Server version	5.5.35-0ubuntu0.12.04.2

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

--
-- Table structure for table `battery_info`
--

DROP TABLE IF EXISTS `battery_info`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `battery_info` (
  `appID` tinyint(3) unsigned NOT NULL,
  `deviceID` varchar(40) NOT NULL,
  `sessionID` double unsigned NOT NULL,
  `serverTime` timestamp NOT NULL DEFAULT '0000-00-00 00:00:00',
  `sequenceNumber` smallint(5) unsigned NOT NULL,
  `level` tinyint(3) unsigned NOT NULL,
  `temp` smallint(5) unsigned NOT NULL,
  `voltage` smallint(5) unsigned NOT NULL,
  `plugged` tinyint(4) NOT NULL,
  KEY `Index_1` (`deviceID`) USING BTREE,
  KEY `Index_2` (`deviceID`,`sessionID`) USING BTREE,
  KEY `Index_3` (`serverTime`) USING BTREE,
  KEY `Index_4` (`level`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `downloads`
--

DROP TABLE IF EXISTS `downloads`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `downloads` (
  `appID` tinyint(3) unsigned NOT NULL,
  `deviceID` varchar(40) NOT NULL,
  `sessionID` double unsigned NOT NULL,
  `serverTime` timestamp NOT NULL DEFAULT '0000-00-00 00:00:00',
  `timeSinceStart` int(10) unsigned NOT NULL DEFAULT '0',
  `txBytes` int(10) unsigned NOT NULL DEFAULT '0',
  `rxBytes` int(10) unsigned NOT NULL DEFAULT '0',
  `duration` int(10) unsigned NOT NULL DEFAULT '0',
  KEY `Index_1` (`deviceID`) USING BTREE,
  KEY `Index_2` (`deviceID`,`sessionID`) USING BTREE,
  KEY `Index_3` (`serverTime`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `event_count`
--

DROP TABLE IF EXISTS `event_count`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `event_count` (
  `appID` tinyint(3) unsigned NOT NULL,
  `deviceID` varchar(40) NOT NULL,
  `sessionID` double unsigned NOT NULL,
  `serverTime` timestamp NOT NULL DEFAULT '0000-00-00 00:00:00',
  `eventID` smallint(6) NOT NULL DEFAULT '-1',
  `count` smallint(5) unsigned NOT NULL DEFAULT '0',
  KEY `Index_1` (`deviceID`) USING BTREE,
  KEY `Index_2` (`deviceID`,`sessionID`) USING BTREE,
  KEY `Index_3` (`serverTime`) USING BTREE,
  KEY `Index_4` (`eventID`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `event_strings`
--

DROP TABLE IF EXISTS `event_strings`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `event_strings` (
  `appID` tinyint(3) unsigned NOT NULL,
  `deviceID` varchar(40) NOT NULL,
  `sessionID` double unsigned NOT NULL,
  `serverTime` timestamp NOT NULL DEFAULT '0000-00-00 00:00:00',
  `timeSinceStart` int(10) unsigned NOT NULL DEFAULT '0',
  `eventID` smallint(6) NOT NULL DEFAULT '-1',
  `value` varchar(40) NOT NULL,
  KEY `Index_1` (`deviceID`) USING BTREE,
  KEY `Index_2` (`deviceID`,`sessionID`) USING BTREE,
  KEY `Index_3` (`serverTime`) USING BTREE,
  KEY `Index_4` (`eventID`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `event_values`
--

DROP TABLE IF EXISTS `event_values`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `event_values` (
  `appID` tinyint(3) unsigned NOT NULL,
  `deviceID` varchar(40) NOT NULL,
  `sessionID` double unsigned NOT NULL,
  `serverTime` timestamp NOT NULL DEFAULT '0000-00-00 00:00:00',
  `timeSinceStart` int(10) unsigned NOT NULL DEFAULT '0',
  `eventID` smallint(6) NOT NULL DEFAULT '-1',
  `value` double NOT NULL DEFAULT '0',
  KEY `Index_1` (`deviceID`) USING BTREE,
  KEY `Index_2` (`deviceID`,`sessionID`) USING BTREE,
  KEY `Index_3` (`serverTime`) USING BTREE,
  KEY `Index_4` (`eventID`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `locations`
--

DROP TABLE IF EXISTS `locations`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `locations` (
  `appID` tinyint(3) unsigned NOT NULL,
  `deviceID` varchar(40) NOT NULL,
  `sessionID` double unsigned NOT NULL,
  `serverTime` timestamp NOT NULL DEFAULT '0000-00-00 00:00:00',
  `locationlat` double NOT NULL,
  `locationlng` double NOT NULL,
  `countryCode` varchar(5) NOT NULL,
  `adminArea` varchar(20) DEFAULT NULL,
  KEY `Index_1` (`deviceID`) USING BTREE,
  KEY `Index_2` (`deviceID`,`sessionID`) USING BTREE,
  KEY `Index_3` (`serverTime`) USING BTREE,
  KEY `Index_4` (`countryCode`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `pingsrecorded`
--

DROP TABLE IF EXISTS `pingsrecorded`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `pingsrecorded` (
  `rtt` double NOT NULL,
  `appID` tinyint(3) unsigned NOT NULL,
  `deviceID` varchar(40) CHARACTER SET ascii NOT NULL,
  `sessionID` double unsigned NOT NULL,
  `serverTime` timestamp NOT NULL DEFAULT '0000-00-00 00:00:00',
  `sequenceNumber` smallint(5) unsigned DEFAULT NULL,
  `platform` tinyint(4) DEFAULT NULL,
  `transportType` enum('tcp','udp') NOT NULL,
  `activeNetwork` tinyint(4) NOT NULL,
  `activeSubType` tinyint(4) DEFAULT NULL,
  `signalStrength` smallint(6) DEFAULT NULL,
  `cellularAvailability` tinyint(4) DEFAULT NULL,
  `isConnectedToCellular` tinyint(4) DEFAULT NULL,
  `cellularState` tinyint(4) DEFAULT NULL,
  `cellularSubType` tinyint(4) DEFAULT NULL,
  `wifiSignalStrength` tinyint(4) DEFAULT NULL,
  `wifiSpeed` smallint(6) DEFAULT NULL,
  `wifiAvailability` tinyint(4) DEFAULT NULL,
  `isConnectedToWifi` tinyint(4) DEFAULT NULL,
  `wifiState` tinyint(4) DEFAULT NULL,
  `wifiSubType` tinyint(4) DEFAULT NULL,
  KEY `Index_1` (`serverTime`) USING BTREE,
  KEY `Index_2` (`appID`) USING BTREE,
  KEY `Index_3` (`rtt`) USING BTREE,
  KEY `Index_4` (`deviceID`,`sessionID`) USING BTREE,
  KEY `Index_5` (`signalStrength`) USING BTREE,
  KEY `Index_6` (`wifiSignalStrength`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `resourceusage_info`
--

DROP TABLE IF EXISTS `resourceusage_info`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `resourceusage_info` (
  `appID` tinyint(3) unsigned NOT NULL,
  `deviceID` varchar(40) NOT NULL,
  `sessionID` double unsigned NOT NULL,
  `serverTime` timestamp NOT NULL DEFAULT '0000-00-00 00:00:00',
  `sequenceNumber` smallint(5) unsigned NOT NULL,
  `cpuUsage` tinyint(3) unsigned NOT NULL,
  `rss` double unsigned NOT NULL,
  `bogomips` double unsigned NOT NULL,
  `avgLoadOneMin` double unsigned NOT NULL,
  `runningProcs` tinyint(3) unsigned NOT NULL,
  `totalProcs` smallint(5) unsigned NOT NULL,
  `memTotalAvail` smallint(6) NOT NULL,
  `totalCpuUsage` tinyint(3) unsigned NOT NULL,
  `screenBrightness` tinyint(3) unsigned NOT NULL,
  `isScreenOn` tinyint(1) NOT NULL,
  `isAudioSpeakerOn` tinyint(1) NOT NULL,
  `isAudioWiredHeadsetOn` tinyint(1) NOT NULL,
  `audioLevel` tinyint(3) unsigned NOT NULL,
  KEY `Index_1` (`deviceID`) USING BTREE,
  KEY `Index_2` (`deviceID`,`sessionID`) USING BTREE,
  KEY `Index_3` (`serverTime`) USING BTREE,
  KEY `Index_4` (`cpuUsage`) USING BTREE,
  KEY `Index_5` (`bogomips`) USING BTREE,
  KEY `Index_6` (`rss`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `sessions`
--

DROP TABLE IF EXISTS `sessions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `sessions` (
  `appID` tinyint(3) unsigned NOT NULL,
  `deviceID` varchar(40) NOT NULL,
  `sessionID` double unsigned NOT NULL,
  `applicationUID` varchar(40) NOT NULL,
  `serverTime` timestamp NOT NULL DEFAULT '0000-00-00 00:00:00',
  `start` datetime NOT NULL,
  `end` datetime NOT NULL,
  `length` int(10) unsigned NOT NULL,
  `totalTxBytes` int(10) unsigned NOT NULL,
  `totalRxBytes` int(10) unsigned NOT NULL,
  `mobileTxBytes` int(10) unsigned NOT NULL,
  `mobileRxBytes` int(10) unsigned NOT NULL,
  `appTxBytes` int(10) unsigned NOT NULL,
  `appRxBytes` int(10) unsigned NOT NULL,
  `platform` tinyint(4) DEFAULT NULL,
  `batteryTechnology` varchar(20) NOT NULL,
  `isForceClosed` tinyint(4) NOT NULL,
  KEY `Index_1` (`deviceID`) USING BTREE,
  KEY `Index_2` (`applicationUID`) USING BTREE,
  KEY `Index_3` (`deviceID`,`sessionID`,`applicationUID`) USING BTREE,
  KEY `Index_4` (`serverTime`) USING BTREE,
  KEY `Index_5` (`start`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `system_device_info`
--

DROP TABLE IF EXISTS `system_device_info`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `system_device_info` (
  `appID` tinyint(3) unsigned NOT NULL,
  `deviceID` varchar(40) NOT NULL,
  `sessionID` double unsigned NOT NULL,
  `serverTime` timestamp NOT NULL DEFAULT '0000-00-00 00:00:00',
  `cellularCarrier` varchar(40) NOT NULL,
  `ip` varchar(20) NOT NULL,
  `osVersion` varchar(40) NOT NULL,
  `osBuild` varchar(40) NOT NULL,
  `osAPI` varchar(40) NOT NULL,
  `deviceModel` varchar(40) NOT NULL,
  `deviceProduct` varchar(40) NOT NULL,
  `processor` varchar(40) NOT NULL,
  `memTotal` smallint(5) unsigned NOT NULL,
  `screenWidth` smallint(5) unsigned NOT NULL,
  `screenHeight` smallint(5) unsigned NOT NULL,
  `densityDpi` smallint(5) unsigned NOT NULL,
  `screenXDpi` smallint(5) unsigned NOT NULL,
  `screenYDpi` smallint(5) unsigned NOT NULL,
  `isGpsLocationOn` tinyint(1) NOT NULL,
  `isNetworkLocationOn` tinyint(1) NOT NULL,
  `activeNetwork` tinyint(4) unsigned NOT NULL DEFAULT '0',
  `activeSubType` tinyint(4) unsigned NOT NULL DEFAULT '0',
  KEY `Index_1` (`deviceID`) USING BTREE,
  KEY `Index_2` (`deviceID`,`sessionID`) USING BTREE,
  KEY `Index_3` (`serverTime`) USING BTREE,
  KEY `Index_4` (`cellularCarrier`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
/*!40101 SET character_set_client = @saved_cs_client */;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- Dump completed on 2014-04-08 15:36:11
