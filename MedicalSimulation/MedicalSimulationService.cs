using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

namespace MedicalSimulation
{
    public class MedicalSimulationService
    {
        private readonly ILogger<MedicalSimulationService> _logger;
        public MedicalSimulationService(ILogger<MedicalSimulationService> logger)
        {
            _logger = logger;
        }

        private readonly int _simulationCount = 500;
        public MedicalSimulationData? SimulateMedicalFloor(InputMedicalSimulationData inputData)
        {
            var simulationData = new MedicalSimulationData();
            InputMedicalSimulationDataValidator validator = new InputMedicalSimulationDataValidator();
            ValidationResult result = validator.Validate(inputData);
            if (!result.IsValid)
            {
                _logger.LogError(string.Join(',',result.Errors));
                return null;
            }

            // setup patients per room
            simulationData.SetUpRooms(inputData.numberOfPatients, inputData.numberOfRooms);
            

            for (int workers = inputData.minWorkers; workers <= inputData.maxWorkers; workers++)
            {
                var workerCount = new WorkerCount()
                {
                    numWorkers = workers,
                    supervisorRatio = inputData.supervisorToWorkerRatio,
                    deskWorkers = inputData.deskWorkers,      
                };
                simulationData.WorkerCount.Add(workerCount);

                // do 500 simulations for each workerd
                for(int i = 0; i< _simulationCount; i++) 
                {
                    
                    var validSenerio = simulationData.DistrubutePatientsToWorkers(workers);
                    if (!validSenerio)
                    {
                        return null;
                    }

                    SimulateWorkersWorking(inputData,workers,ref simulationData);
                }
            }

            return simulationData;
        }

        private void SimulateWorkersWorking(InputMedicalSimulationData inputData, int numWorkers, ref MedicalSimulationData simulationData, int? seed = null)
        {
            Random random;
            if (seed == null)
            {
                random = new Random();
            }
            else
            {
                random = new Random((int)seed);
            }
            
            List<Output> outputList = new List<Output>();
            for(int i =0; i < numWorkers; i++)
            {
                var tempOutPut = new Output();
                for(int p = 0; p < simulationData.Workers[i].numberOfPatients;p++)
                {
                    // use triangle dist to simulate working     
                    tempOutPut.TimePerPatient.Add(TrinangularDist(inputData.patientCareMinimumTime, inputData.patientCareMaximumTime, random));
                }



                // add time between rooms
                // add all together
                tempOutPut.timePerRound = tempOutPut.TimePerPatient.Sum() + (inputData.timeBetweenRooms * simulationData.Workers[i].numberOfRooms);
                tempOutPut.numberOfRooms = simulationData.Workers[i].numberOfRooms;
                tempOutPut.WorkerId = i;
                
                // Add to output object list
                outputList.Add(tempOutPut);
            }

            // moves object list to simulation data object
            simulationData.Outputs.Add(outputList.ToList());
        }


        /// <summary>
        /// Create a simple trinagle distribution https://en.wikipedia.org/wiki/Triangular_distribution
        /// </summary>
        /// <param name="a">min of triangle values</param>
        /// <param name="b">max of trinagle values</param>
        /// <param name="rnd">random number generator passed in</param>
        /// <returns>double that value of triangle distribution</returns>
        private double TrinangularDist(double a, double b, Random rnd)
        {
            var r = rnd.NextDouble();

            var c = (a + b) / 2;

            if (r >= 0 && r < 0.5)
            {
                return a + Math.Sqrt(r * (b - a) * (c - a));
            }
            else if (r >= 0.5 && r < 1)
            {
                return b - Math.Sqrt((1 - r) * (b - a) * (b - c));
            }
            else
            {
                return c;
            }
        }

        public List<List<List<Output>>> SplitList(List<List<Output>> location, int size = 500)
        {
            var finalList = new List<List<List<Output>>>();

            for (int i = 0; i < location.Count; i += size)
            {
                finalList.Add(location.GetRange(i, Math.Min(size, location.Count - i)));
            }
            return finalList;
        }

    }
    public class MedicalSimulationData
    {
        public List<WorkerData> Workers = new List<WorkerData>();
        public List<RoomData> Rooms = new List<RoomData>();
        public List<List<Output>> Outputs = new List<List<Output>>();
        public List<WorkerCount> WorkerCount = new List<WorkerCount>();
        public int deskWorkers;
        public double supervisorRatio;
        public string Error=string.Empty;
        
        
        
           
        
        /// <summary>
        /// This method distributes patients to rooms at an even rate. Should only be called once
        /// </summary>
        /// <param name="numberOfPatient"></param>
        /// <param name="numberOfRooms"></param>
        public void SetUpRooms(int numberOfPatient,int numberOfRooms)
        {
            var tempRooms = new RoomData[numberOfRooms];
            for(int i = 0; i<tempRooms.Length; i++)
            {
                tempRooms[i] = new RoomData();
            }
            
            // distributes patients to rooms, some roooms may not have any patients
            for (int i = 0; i < numberOfPatient; i++)
            {
                tempRooms[i % numberOfRooms].numberOfPatients++;
            }

            // add rooms to simulation object
            Rooms.AddRange(tempRooms);
        }

        /// <summary>
        /// Distribute patients evenly workers in a ordered fashion. Should only be called once
        /// </summary>
        /// <param name="numberOfWorkers"></param>
        /// <returns></returns>
        public bool DistrubutePatientsToWorkers(int numberOfWorkers)
        {
            this.Workers.Clear();
            var tempWorkers = new WorkerData[numberOfWorkers];
            for(int i = 0; i < tempWorkers.Length; i++)
            {
                tempWorkers[i] = new WorkerData();
            }

            var activeRooms = Rooms.Count(x => x.numberOfPatients > 0);

            double workerToPatientRatio = numberOfWorkers / Rooms.Sum(x => x.numberOfPatients);
            var totalPatients = Rooms.Sum(x => x.numberOfPatients);
            int patientToWoker = totalPatients / numberOfWorkers;
            int patientsLeft = totalPatients % numberOfWorkers;
            

            // This is always valid as more wokers than patients
            if (workerToPatientRatio > 1.0)
            {
                Error = "More Workers than Patient No need for simulation";
                return false;
            }


            // each worker gets 1 room 
            if (activeRooms == numberOfWorkers)
            {
                var index = 0;
                // distribute one worker per room at minimum some will have multiple
                foreach (var room in Rooms)
                {
                    if (room.numberOfPatients <= 0)
                    {
                        continue;
                    }

                    // assign a worker a room
                    tempWorkers[index].numberOfPatients += room.numberOfPatients;
                    tempWorkers[index].numberOfRooms++;
                    index++;
                }
            }
            else
            {
                /* Situations
                   - workers will each get rooms 1-x and then split rooms with other workers x-end as those will have less
                   - workers will have multiple rooms and have split rooms after that
                    try to find number of patients per worker and then add the rooms in at the end to distribute the work better among later workers

                */
                // will get number of patients per doctor
                var workerWorkArray = new int[numberOfWorkers];
                for (int i = 0; i < workerWorkArray.Length; i++)
                {
                    workerWorkArray[i] = patientToWoker;
                    if(i < patientsLeft)
                    {
                        workerWorkArray[i]++;
                    }
                }

                // loop through the rooms and dish out the rooms based on patients
                int workerIndex = 0;
                int currentWorkerPatients = workerWorkArray[workerIndex];
                foreach(var room in Rooms)
                {
                    int roomPatientsLeft = room.numberOfPatients;
                    while (roomPatientsLeft > 0)
                    {
                        // get current patients left for nurse / doctor
                        currentWorkerPatients = workerWorkArray[workerIndex]- tempWorkers[workerIndex].numberOfPatients;

                        // worker will work in another room
                        tempWorkers[workerIndex].numberOfRooms++;
                        
                        // if current doctors is more than patients in room take all patients and go to next room
                        if(currentWorkerPatients > roomPatientsLeft)
                        {
                            tempWorkers[workerIndex].numberOfPatients += roomPatientsLeft;
                            
                            roomPatientsLeft = 0;
                        }
                        // if they are both equal then add patients to doctor and move room and to next doctor
                        else if(currentWorkerPatients == roomPatientsLeft)
                        {
                            tempWorkers[workerIndex].numberOfPatients += currentWorkerPatients;

                            roomPatientsLeft = 0;

                            workerIndex++;
                        }
                        // room has more patients then doctor so add patients to doctor and move worker index for next loop
                        else
                        {
                            tempWorkers[workerIndex].numberOfPatients += currentWorkerPatients;

                            roomPatientsLeft -= currentWorkerPatients;

                            workerIndex++;
                        }
                    }
                }
            }

            this.Workers.AddRange(tempWorkers);
            // this is valid simulation case
            return true;
        }
    }
    #region Classes
    public class WorkerData
    {
        public int numberOfPatients;
        public int numberOfRooms;
    }

    public class RoomData
    {
        public int numberOfPatients;
    };

    public class WorkerCount
    {
        public int numWorkers;
        public double supervisorRatio;
        public int deskWorkers;
        public int numberOfSupervisors => Convert.ToInt32( Math.Ceiling(numWorkers* supervisorRatio));
        public int totalWorkers => numWorkers + deskWorkers + numberOfSupervisors;
    }
    public class Output
    {
        public List<double> TimePerPatient = new List<double>();
        public int numberOfRooms;
        public int numberOfPatients;
        public double timePerRound;
        public int WorkerId;
    }

    public record InputMedicalSimulationData(int minWorkers, int maxWorkers, int deskWorkers, int numberOfPatients, int numberOfRooms, double supervisorToWorkerRatio, double timeBetweenRooms, double patientCareMaximumTime, double patientCareMinimumTime);

    public class InputMedicalSimulationDataValidator : AbstractValidator<InputMedicalSimulationData>
    {
        public InputMedicalSimulationDataValidator()
        {
            RuleFor(data => data.minWorkers).GreaterThan(0);
            RuleFor(data => data.maxWorkers).GreaterThan(data=> data.minWorkers);
            RuleFor(data => data.numberOfPatients).GreaterThan(0);
            RuleFor(data => data.numberOfRooms).GreaterThan(0);
            RuleFor(data => data.supervisorToWorkerRatio).GreaterThan(0).LessThan(1);
            RuleFor(data => data.timeBetweenRooms).GreaterThanOrEqualTo(0);
            RuleFor(data => data.patientCareMinimumTime).GreaterThan(0);
            RuleFor(data => data.patientCareMaximumTime).GreaterThanOrEqualTo(x => x.patientCareMinimumTime).GreaterThan(0);
        }
    }


    #endregion

    #region Extension Class
    public static class Extension
    {
        public static IEnumerable<List<T>> SplitList<T>(List<T> locations, int nSize = 30)
        {
            for (int i = 0; i < locations.Count; i += nSize)
            {
                yield return locations.GetRange(i, Math.Min(nSize, locations.Count - i));
            }
        }
    }
    #endregion
}
