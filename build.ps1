$container = 'hcup/odin'
$tag = 'stor.highloadcup.ru/accounts/fox_flyer'

minikube docker-env | Invoke-Expression

Set-Location src
.\build.cmd
Set-Location ..
docker build -t $container .
# docker run -it --rm -p 80:80 $container
# docker login stor.highloadcup.ru
docker tag $container $tag
docker push $tag