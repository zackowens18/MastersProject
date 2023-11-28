using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Linq;
using Moq;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using MedicalSimulation;

namespace MedicalSimulationTest
{
    [TestClass]
    public class MedicalSimulationTest
    {
        #region SetUpRoomsTest
        [TestMethod]
        public void TestSetUpRooms_EqualRoomsAndPatients()
        {
            // Arrange
            var medicalSim = new MedicalSimulationData();
            int numPatients = 100;
            int numRooms = 100;

            // Act 
            medicalSim.SetUpRooms(numPatients, numRooms);

            // Assert
            medicalSim.Rooms.Should().HaveCount(numRooms);
            medicalSim.Rooms.Select(x =>x.numberOfPatients).Should().AllBeEquivalentTo(1);

        }

        [TestMethod]
        public void TestSetUpRoom_MoreRoomsThanPatients()
        {
            // Arrange
            var medicalSim = new MedicalSimulationData();
            int numPatients = 100;
            int numRooms = 150;

            // Act 
            medicalSim.SetUpRooms(numPatients, numRooms);

            // Assert
            medicalSim.Rooms.Should().HaveCount(numRooms);
            medicalSim.Rooms.Select(x => x.numberOfPatients).Take(100).Should().AllBeEquivalentTo(1);
            medicalSim.Rooms.Select(x => x.numberOfPatients).Skip(100).Take(50).Should().AllBeEquivalentTo(0);
        }

        [TestMethod]
        public void TestSetUpRooms_LessRoomsThanPatients()
        {
            // Arrange
            var medicalSim = new MedicalSimulationData();
            int numPatients = 150;
            int numRooms = 100;

            // Act 
            medicalSim.SetUpRooms(numPatients, numRooms);

            // Assert
            medicalSim.Rooms.Should().HaveCount(numRooms);
            medicalSim.Rooms.Select(x => x.numberOfPatients).Take(50).Should().AllBeEquivalentTo(2);
            medicalSim.Rooms.Select(x => x.numberOfPatients).Skip(50).Take(50).Should().AllBeEquivalentTo(1);        }
        #endregion

        #region DistributePatientsToWorkers

        [TestMethod]
        public void TestDistributePatientsToWorkers_EqualWorkerToPatientsToRooms()
        {
            // Arrange
            var medicalSim = new MedicalSimulationData();
            int numPatients = 100;
            int numRooms = 100;
            int numWorkers = 100;

            // Act 
            medicalSim.SetUpRooms(numPatients, numRooms);
            var isValid = medicalSim.DistrubutePatientsToWorkers(numWorkers);

            // Assert
            Assert.IsTrue(isValid);
            medicalSim.Rooms.Should().HaveCount(numRooms);
            medicalSim.Workers.Select(x => x.numberOfPatients).Sum().Should().Be(numPatients);
            medicalSim.Workers.Select(x => x.numberOfRooms).Sum().Should().Be(numRooms);
            
        }


        [TestMethod]
        public void TestDistributePatientsToWorkers_MoreWorkerThanPatientsSameRooms()
        {
            // Arrange
            var medicalSim = new MedicalSimulationData();
            int numPatients = 100;
            int numRooms = 100;
            int numWorkers = 200;

            // Act 
            medicalSim.SetUpRooms(numPatients, numRooms);
            var isValid = medicalSim.DistrubutePatientsToWorkers(numWorkers);

            // Assert
            Assert.IsFalse(isValid);
        }


        [TestMethod]
        public void TestDistributePatientsToWorkers_LessWorkerThanPatientsSameRooms()
        {
            // Arrange
            var medicalSim = new MedicalSimulationData();
            int numPatients = 400;
            int numRooms = 200;
            int numWorkers = 50;

            // Act 
            medicalSim.SetUpRooms(numPatients, numRooms);
            var isValid = medicalSim.DistrubutePatientsToWorkers(numWorkers);

            // Assert
            Assert.IsTrue(isValid);
            medicalSim.Rooms.Should().HaveCount(numRooms);
            medicalSim.Workers.Select(x => x.numberOfPatients).Sum().Should().Be(numPatients);
            medicalSim.Workers.Select(x => x.numberOfRooms).Sum().Should().Be(numRooms);
        }

        [TestMethod]
        public void TestDistributePatientsToWorkers_LessWorkerThanPatientsDifferentRooms()
        {
            // Arrange
            var medicalSim = new MedicalSimulationData();
            int numPatients = 500;
            int numRooms = 20;
            int numWorkers = 50;

            // Act 
            medicalSim.SetUpRooms(numPatients, numRooms);
            var isValid = medicalSim.DistrubutePatientsToWorkers(numWorkers);

            // Assert
            Assert.IsTrue(isValid);
            medicalSim.Rooms.Should().HaveCount(numRooms);
            medicalSim.Workers.Select(x => x.numberOfPatients).Sum().Should().Be(numPatients);
            medicalSim.Workers.Select(x => x.numberOfRooms).Sum().Should().Be(numRooms*3);
        }
        #endregion

        #region Simulation
        [TestMethod]
        public void TestSimulation_1()
        {
            double supervisorToWorkerRatio = 0.5;
            int deskWorkers = 5;
            int maxWorkers = 10;
            int minWorkers = 8;
            int numberOfPatients = 20;
            double patientCareMaximumTime = 5;
            double patientCareMinimumTime = 3;
            int numberOfRooms = 10;
            double timeBetweenRooms = 2;

            var logger = Mock.Of<ILogger<MedicalSimulationService>>();
            InputMedicalSimulationData input = new InputMedicalSimulationData(minWorkers, maxWorkers, deskWorkers,numberOfPatients,numberOfRooms,supervisorToWorkerRatio,timeBetweenRooms, patientCareMaximumTime,patientCareMinimumTime);
            MedicalSimulationService medicalSimulationService = new MedicalSimulationService(logger);
            var outputdata = medicalSimulationService.SimulateMedicalFloor(input);

            Assert.IsNotNull(outputdata);
            outputdata.Outputs.Should().HaveCount(500*(maxWorkers - minWorkers+1));
            outputdata.Outputs[0].Count.Should().Be(8);
            outputdata.Outputs[0+500].Count.Should().Be(9);
            outputdata.Outputs[0+1_000].Count.Should().Be(10);
            outputdata.Outputs[0][0].timePerRound.Should().BeGreaterThan(patientCareMinimumTime * 2);
            outputdata.WorkerCount.Count().Should().Be(3);
            outputdata.WorkerCount[0].totalWorkers.Should().Be(8 + 4 + 5);
            outputdata.WorkerCount[1].totalWorkers.Should().Be(9 + 5 + 5);
        }

        [TestMethod]
        public void TestSimulation_GroupingLogic()
        {
            double supervisorToWorkerRatio = 0.5;
            int deskWorkers = 5;
            int maxWorkers = 10;
            int minWorkers = 8;
            int numberOfPatients = 20;
            double patientCareMaximumTime = 5;
            double patientCareMinimumTime = 3;
            int numberOfRooms = 10;
            double timeBetweenRooms = 2;

            var logger = Mock.Of<ILogger<MedicalSimulationService>>();
            List<MedicalSummary>? medicalSummaries = new List<MedicalSummary>();

            InputMedicalSimulationData input = new InputMedicalSimulationData(minWorkers, maxWorkers, deskWorkers, numberOfPatients, numberOfRooms, supervisorToWorkerRatio, timeBetweenRooms, patientCareMaximumTime, patientCareMinimumTime);
            MedicalSimulationService medicalSimulationService = new MedicalSimulationService(logger);
            var outputdata = medicalSimulationService.SimulateMedicalFloor(input);


            var outputsPerSimulation = medicalSimulationService.SplitList(outputdata.Outputs,500);
            
            foreach (var list in outputsPerSimulation)
            {
                var allOutputsofSingleSimulation = list.SelectMany(x=>x).ToList();
                var groupedWorkers = allOutputsofSingleSimulation.GroupBy(x => x.WorkerId);
                medicalSummaries.AddRange(groupedWorkers.Select(x => new MedicalSummary()
                {
                    averageRountTime = x.Average(a => a.timePerRound),
                    maxRoundTime = x.Max(a => a.timePerRound),
                    minRoundTime = x.Min(a => a.timePerRound),
                    workerNumber = x.Key
                }).ToList());
            }
        }
        #endregion

        #region InternalClasses
        internal class MedicalSummary
        {
            public int workerNumber;
            public double averageRountTime, maxRoundTime, minRoundTime;
        }

        #endregion



        
    }
}