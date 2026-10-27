# Must located in the parent of src folder, same level with scripts folder

docker build -t demowebapi:dev -f .\src\Demo.WebApi\Dockerfile .

docker run --rm `
	-p 21210:8080 `
	-e DbProvider__Default=Sqlite `
	-e DbProvider__DbScriptsPath=../scripts `
	-e FileStoragePath="../var/data" `
	-v "D:\SourceCode\Github\public\dynamic-sql\src\Demo.WebApi\FileStorage:/var/data" `
	demowebapi:dev
