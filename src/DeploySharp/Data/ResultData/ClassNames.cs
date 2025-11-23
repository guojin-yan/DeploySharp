using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{

    public class ClassNames
    {
        /// <summary>
        /// COCO 数据集的 80 个类别名称映射。
        /// Key: 类别ID (从0开始)
        /// Value: 类别名称
        /// </summary>
        public static readonly Dictionary<int, string> CocoClassNames = new Dictionary<int, string>
        {
            { 0, "person" },         { 1, "bicycle" },       { 2, "car" },            { 3, "motorcycle" },      { 4, "airplane" },
            { 5, "bus" },            { 6, "train" },         { 7, "truck" },          { 8, "boat" },            { 9, "traffic light" },
            { 10, "fire hydrant" },  { 11, "stop sign" },    { 12, "parking meter" }, { 13, "bench" },          { 14, "bird" },
            { 15, "cat" },           { 16, "dog" },          { 17, "horse" },         { 18, "sheep" },          { 19, "cow" },
            { 20, "elephant" },      { 21, "bear" },         { 22, "zebra" },         { 23, "giraffe" },        { 24, "backpack" },
            { 25, "umbrella" },      { 26, "handbag" },      { 27, "tie" },           { 28, "suitcase" },       { 29, "frisbee" },
            { 30, "skis" },          { 31, "snowboard" },    { 32, "sports ball" },   { 33, "kite" },           { 34, "baseball bat" },
            { 35, "baseball glove" },{ 36, "skateboard" },   { 37, "surfboard" },     { 38, "tennis racket" },  { 39, "bottle" },
            { 40, "wine glass" },    { 41, "cup" },          { 42, "fork" },          { 43, "knife" },          { 44, "spoon" },
            { 45, "bowl" },          { 46, "banana" },       { 47, "apple" },         { 48, "sandwich" },       { 49, "orange" },
            { 50, "broccoli" },      { 51, "carrot" },       { 52, "hot dog" },       { 53, "pizza" },          { 54, "donut" },
            { 55, "cake" },          { 56, "chair" },        { 57, "couch" },         { 58, "potted plant" },   { 59, "bed" },
            { 60, "dining table" },  { 61, "toilet" },       { 62, "tv" },            { 63, "laptop" },         { 64, "mouse" },
            { 65, "remote" },        { 66, "keyboard" },     { 67, "cell phone" },    { 68, "microwave" },      { 69, "oven" },
            { 70, "toaster" },       { 71, "sink" },         { 72, "refrigerator" },  { 73, "book" },           { 74, "clock" },
            { 75, "vase" },          { 76, "scissors" },     { 77, "teddy bear" },    { 78, "hair drier" },     { 79, "toothbrush" }
        };
        /// <summary>
        /// PASCAL VOC 数据集的 20 个类别名称映射。
        /// Key: 类别ID (从1开始, 0通常为背景)
        /// Value: 类别名称
        /// </summary>
        public static readonly Dictionary<int, string> VocClassNames = new Dictionary<int, string>
        {
            { 1, "aeroplane" }, { 2, "bicycle" }, { 3, "bird" }, { 4, "boat" }, { 5, "bottle" },
            { 6, "bus" }, { 7, "car" }, { 8, "cat" }, { 9, "chair" }, { 10, "cow" },
            { 11, "diningtable" }, { 12, "dog" }, { 13, "horse" }, { 14, "motorbike" }, { 15, "person" },
            { 16, "pottedplant" }, { 17, "sheep" }, { 18, "sofa" }, { 19, "train" }, { 20, "tvmonitor" }
        };
        /// <summary>
        /// CIFAR-10 数据集的 10 个类别名称映射。
        /// Key: 类别ID (从0开始)
        /// Value: 类别名称
        /// </summary>
        public static readonly Dictionary<int, string> Cifar10ClassNames = new Dictionary<int, string>
        {
            { 0, "airplane" }, { 1, "automobile" }, { 2, "bird" }, { 3, "cat" }, { 4, "deer" },
            { 5, "dog" }, { 6, "frog" }, { 7, "horse" }, { 8, "ship" }, { 9, "truck" }
        };

        /// <summary>
        /// ImageNet (ILSVRC 2012) 数据集的 1000 个类别名称映射。
        /// Key: 类别ID (从0开始)
        /// Value: 类别名称 (或WNID描述)
        /// </summary>
        public static readonly Dictionary<int, string> ImageNetClassNames = new Dictionary<int, string>
        {
            {0, "tench, Tinca tinca"}, {1, "goldfish, Carassius auratus"}, {2, "great white shark, white shark, man-eater, man-eating shark, Carcharodon carcharias"}, {3, "tiger shark, Galeocerdo cuvieri"}, {4, "hammerhead, hammerhead shark"}, {5, "electric ray, crampfish, numbfish, torpedo"}, {6, "stingray"}, {7, "cock"}, {8, "hen"}, {9, "ostrich, Struthio camelus"},
            {10, "brambling, Fringilla montifringilla"}, {11, "goldfinch, Carduelis carduelis"}, {12, "house finch, linnet, Carpodacus mexicanus"}, {13, "junco, snowbird"}, {14, "indigo bunting, indigo finch, indigo bird, Passerina cyanea"}, {15, "robin, American robin, Turdus migratorius"}, {16, "bulbul"}, {17, "jay"}, {18, "magpie"}, {19, "chickadee"},
            {20, "water ouzel, dipper"}, {21, "kite"}, {22, "bald eagle, American eagle, Haliaeetus leucocephalus"}, {23, "vulture"}, {24, "great grey owl, great gray owl, Strix nebulosa"}, {25, "European fire salamander, Salamandra salamandra"}, {26, "common newt, Triturus vulgaris"}, {27, "eft"}, {28, "spotted salamander, Ambystoma maculatum"}, {29, "axolotl, mud puppy, Ambystoma mexicanum"},
            {30, "bullfrog, Rana catesbeiana"}, {31, "tree frog, tree-frog"}, {32, "tailed frog, bell toad, ribbed toad, Ascaphus trui"}, {33, "loggerhead, loggerhead turtle, Caretta caretta"}, {34, "leatherback turtle, leathery turtle, Dermochelys coriacea"}, {35, "mud turtle"}, {36, "terrapin"}, {37, "box turtle, box tortoise"}, {38, "banded gecko"}, {39, "common iguana, iguana, Iguana iguana"},
            {40, "American chameleon, anole, Anolis carolinensis"}, {41, "whiptail, whiptail lizard"}, {42, "agama"}, {43, "frilled lizard, Chlamydosaurus kingii"}, {44, "alligator lizard"}, {45, "Gila monster, Heloderma suspectum"}, {46, "green lizard, Lacerta viridis"}, {47, "African chameleon, Chamaeleo chamaeleon"}, {48, "Komodo dragon, Komodo lizard, dragon lizard, Varanus komodoensis"}, {49, "African crocodile, Nile crocodile, Crocodylus niloticus"},
            {50, "American alligator, Alligator mississipiensis"}, {51, "triceratops"}, {52, "thunder snake, worm snake, Carphophis amoenus"}, {53, "ringneck snake, ring-necked snake, ring snake"}, {54, "hognose snake, puff adder, sand viper"}, {55, "green snake, grass snake"}, {56, "king snake, kingsnake"}, {57, "garter snake, grass snake"}, {58, "water snake"}, {59, "vine snake"},
            {60, "night snake, Hypsiglena torquata"}, {61, "boa constrictor, Constrictor constrictor"}, {62, "rock python, rock snake, Python sebae"}, {63, "Indian cobra, Naja naja"}, {64, "green mamba"}, {65, "sea snake"}, {66, "horned viper, cerastes, sand viper, horned asp, Cerastes cornutus"}, {67, "diamondback, diamondback rattlesnake, Crotalus adamanteus"}, {68, "sidewinder, horned rattlesnake, Crotalus cerastes"}, {69, "trilobite"},
            {70, "harvestman, daddy longlegs, Phalangium opilio"}, {71, "scorpion"}, {72, "black and gold garden spider, Argiope aurantia"}, {73, "barn spider, Araneus cavaticus"}, {74, "garden spider, Aranea diademata"}, {75, "black widow, Latrodectus mactans"}, {76, "tarantula"}, {77, "wolf spider, hunting spider"}, {78, "tick"}, {79, "centipede"},
            {80, "black grouse"}, {81, "ptarmigan"}, {82, "ruffed grouse, partridge, Bonasa umbellus"}, {83, "prairie chicken, prairie grouse, prairie fowl"}, {84, "peacock"}, {85, "quail"}, {86, "partridge"}, {87, "grey parrot"}, {88, "macaw"}, {89, "sulphur-crested cockatoo, Kakatoe galerita, Cacatua galerita"},
            {90, "lorikeet"}, {91, "coucal"}, {92, "bee eater"}, {93, "hornbill"}, {94, "hummingbird"}, {95, "jacamar"}, {96, "toucan"}, {97, "drake"}, {98, "red-breasted merganser, Mergus serrator"}, {99, "goose"},
            {100, "black swan, Cygnus atratus"}, {101, "tusker"}, {102, "echidna, spiny anteater, anteater"}, {103, "platypus, duckbill, duckbilled platypus, duck-billed platypus, Ornithorhynchus anatinus"}, {104, "wallaby, brush kangaroo"}, {105, "koala, koala bear, kangaroo bear, native bear, Phascolarctos cinereus"}, {106, "wombat"}, {107, "jellyfish"}, {108, "sea anemone, anemone"}, {109, "brain coral"},
            {110, "flatworm, turbellarian"}, {111, "nematode, nematode worm, roundworm"}, {112, "conch"}, {113, "snail"}, {114, "slug"}, {115, "sea slug, nudibranch"}, {116, "chiton, coat-of-mail shell, sea cradle, polyplacophore"}, {117, "chambered nautilus, pearly nautilus, nautilus"}, {118, "Dungeness crab, Cancer magister"}, {119, "rock crab, Cancer irroratus"},
            {120, "fiddler crab"}, {121, "king crab, Alaska crab, Alaskan king crab, Alaska king crab, Paralithodes camtschatica"}, {122, "American lobster, Northern lobster, Maine lobster, Homarus americanus"}, {123, "spiny lobster, langouste, rock lobster, crawfish, crayfish, sea crawfish"}, {124, "crayfish, crawfish, crawdad, crawdaddy"}, {125, "hermit crab"}, {126, "isopod"}, {127, "white stork, Ciconia ciconia"}, {128, "black stork, Ciconia nigra"}, {129, "spoonbill"},
            {130, "flamingo"}, {131, "little blue heron, Egretta caerulea"}, {132, "American egret, great white heron, Egretta albus"}, {133, "bittern"}, {134, "crane"}, {135, "limpkin, Aramus pictus"}, {136, "European gallinule, Porphyrio porphyrio"}, {137, "American coot, marsh hen, mud hen, water hen, Fulica americana"}, {138, "bustard"}, {139, "ruddy turnstone, Arenaria interpres"},
            {140, "red-backed sandpiper, dunlin, Erolia alpina"}, {141, "redshank, Tringa totanus"}, {142, "dowitcher"}, {143, "oystercatcher, oyster-catcher"}, {144, "pelican"}, {145, "king penguin, Aptenodytes patagonica"}, {146, "albatross, mollymawk"}, {147, "grey whale, gray whale, devilfish, Eschrichtius gibbosus, Eschrichtius robustus"}, {148, "killer whale, killer, orca, grampus, sea wolf, Orcinus orca"}, {149, "dugong, Dugong dugon"},
            {150, "sea lion"}, {151, "Chihuahua"}, {152, "Japanese spaniel"}, {153, "Maltese dog, Maltese terrier, Maltese"}, {154, "Pekinese, Pekingese, Peke"}, {155, "Shih-Tzu"}, {156, "Blenheim spaniel"}, {157, "papillon"}, {158, "toy terrier"}, {159, "Rhodesian ridgeback"},
            {160, "Afghan hound, Afghan"}, {161, "basset, basset hound"}, {162, "beagle"}, {163, "bloodhound, sleuthhound"}, {164, "bluetick"}, {165, "black-and-tan coonhound"}, {166, "Walker hound, Walker foxhound"}, {167, "English foxhound"}, {168, "redbone"}, {169, "borzoi, Russian wolfhound"},
            {170, "Irish wolfhound"}, {171, "Italian greyhound"}, {172, "whippet"}, {173, "Ibizan hound, Ibizan Podenco"}, {174, "Norwegian elkhound, elkhound"}, {175, "otterhound, otter hound"}, {176, "Saluki, gazelle hound"}, {177, "Scottish deerhound, deerhound"}, {178, "Weimaraner"}, {179, "Staffordshire bullterrier, Staffordshire bull terrier"},
            {180, "American Staffordshire terrier, Staffordshire terrier, American pit bull terrier, pit bull terrier"}, {181, "Bedlington terrier"}, {182, "Border terrier"}, {183, "Kerry blue terrier"}, {184, "Irish terrier"}, {185, "Norfolk terrier"}, {186, "Norwich terrier"}, {187, "Yorkshire terrier"}, {188, "wire-haired fox terrier"}, {189, "Lakeland terrier"},
            {190, "Sealyham terrier, Sealyham"}, {191, "Airedale, Airedale terrier"}, {192, "cairn, cairn terrier"}, {193, "Australian terrier"}, {194, "Dandie Dinmont, Dandie Dinmont terrier"}, {195, "Boston bull, Boston terrier"}, {196, "miniature schnauzer"}, {197, "giant schnauzer"}, {198, "standard schnauzer"}, {199, "Scotch terrier, Scottish terrier, Scottie"},
            {200, "Tibetan terrier, chrysanthemum dog"}, {201, "silky terrier, Sydney silky"}, {202, "soft-coated wheaten terrier"}, {203, "West Highland white terrier"}, {204, "Lhasa, Lhasa apso"}, {205, "flat-coated retriever"}, {206, "curly-coated retriever"}, {207, "golden retriever"}, {208, "Labrador retriever"}, {209, "Chesapeake Bay retriever"},
            {210, "German short-haired pointer"}, {211, "vizsla, Hungarian pointer"}, {212, "English setter"}, {213, "Irish setter, red setter"}, {214, "Gordon setter"}, {215, "Brittany spaniel"}, {216, "clumber, clumber spaniel"}, {217, "English springer, English springer spaniel"}, {218, "Welsh springer spaniel"}, {219, "cocker spaniel, English cocker spaniel, cocker"},
            {220, "kelpie"}, {221, "komondor"}, {222, "Old English sheepdog, bobtail"}, {223, "Shetland sheepdog, Shetland sheep dog, Shetland"}, {224, "collie"}, {225, "Border collie"}, {226, "Bouvier des Flandres, Bouviers des Flandres"}, {227, "Rottweiler"}, {228, "German shepherd, German shepherd dog, German police dog, alsatian"}, {229, "Doberman, Doberman pinscher"},
            {230, "miniature pinscher"}, {231, "Greater Swiss Mountain dog"}, {232, "Bernese mountain dog"}, {233, "Appenzeller"}, {234, "EntleBucher"}, {235, "boxer"}, {236, "bull mastiff"}, {237, "Tibetan mastiff"}, {238, "French bulldog"}, {239, "Great Dane"},
            {240, "Saint Bernard, St Bernard"}, {241, "Eskimo dog, husky"}, {242, "malamute, malemute, Alaskan malamute"}, {243, "Siberian husky"}, {244, "dalmatian, coach dog, carriage dog"}, {245, "affenpinscher, monkey pinscher, monkey dog"}, {246, "basenji"}, {247, "pug, pug-dog"}, {248, "Leonberg"}, {249, "Newfoundland, Newfoundland dog"},
            {250, "Great Pyrenees"}, {251, "Samoyed, Samoyede"}, {252, "Pomeranian"}, {253, "chow, chow chow"}, {254, "keeshond"}, {255, "Brabancon griffon"}, {256, "Pembroke, Pembroke Welsh corgi"}, {257, "Cardigan, Cardigan Welsh corgi"}, {258, "toy poodle"}, {259, "miniature poodle"},
            {260, "standard poodle"}, {261, "Mexican hairless"}, {262, "timber wolf, grey wolf, gray wolf, Canis lupus"}, {263, "white wolf, Arctic wolf, Canis lupus tundrarum"}, {264, "red wolf, maned wolf, Canis rufus, Canis niger"}, {265, "coyote, prairie wolf, brush wolf, Canis latrans"}, {266, "dingo, warrigal, warragal, Canis dingo"}, {267, "dhole, Cuon alpinus"}, {268, "African hunting dog, hyena dog, Cape hunting dog, Lycaon pictus"}, {269, "hyena, hyaena"},
            {270, "red fox, Vulpes vulpes"}, {271, "kit fox, Vulpes macrotis"}, {272, "Arctic fox, white fox, Alopex lagopus"}, {273, "grey fox, gray fox, Urocyon cinereoargenteus"}, {274, "tabby, tabby cat"}, {275, "tiger cat"}, {276, "Persian cat"}, {277, "Siamese cat, Siamese"}, {278, "Egyptian cat"}, {279, "cougar, puma, catamount, mountain lion, painter, panther, Felis concolor"},
            {280, "lynx, catamount"}, {281, "leopard, Panthera pardus"}, {282, "snow leopard, ounce, Panthera uncia"}, {283, "jaguar, panther, Panthera onca, Felis onca"}, {284, "lion, king of beasts, Panthera leo"}, {285, "tiger, Panthera tigris"}, {286, "cheetah, chetah, Acinonyx jubatus"}, {287, "brown bear, bruin, Ursus arctos"}, {288, "American black bear, black bear, Ursus americanus, Euarctos americanus"}, {289, "ice bear, polar bear, Ursus Maritimus, Thalarctos maritimus"},
            {290, "sloth bear, Melursus ursinus, Ursus ursinus"}, {291, "mongoose"}, {292, "meerkat, mierkat"}, {293, "tiger beetle"}, {294, "ladybug, ladybeetle, lady beetle, ladybird, ladybird beetle"}, {295, "ground beetle, carabid beetle"}, {296, "long-horned beetle, longicorn, longicorn beetle"}, {297, "leaf beetle, chrysomelid"}, {298, "dung beetle"}, {299, "rhinoceros beetle"},
            {300, "weevil"}, {301, "fly"}, {302, "bee"}, {303, "ant, emmet, pismire"}, {304, "grasshopper, hopper"}, {305, "cricket"}, {306, "walking stick, walkingstick, stick insect"}, {307, "cockroach, roach"}, {308, "mantis, mantid"}, {309, "cicada, cicala"},
            {310, "leafhopper"}, {311, "lacewing, lacewing fly"}, {312, "dragonfly, darning needle, devil's darning needle, sewing needle, snake feeder, snake doctor, mosquito hawk, skeeter hawk"}, {313, "damselfly"}, {314, "admiral"}, {315, "ringlet, ringlet butterfly"}, {316, "monarch, monarch butterfly, milkweed butterfly, Danaus plexippus"}, {317, "cabbage butterfly"}, {318, "sulphur butterfly, sulfur butterfly"}, {319, "lycaenid, lycaenid butterfly"},
            {320, "starfish, sea star"}, {321, "sea urchin"}, {322, "sea cucumber, holothurian"}, {323, "wood rabbit, cottontail, cottontail rabbit"}, {324, "hare"}, {325, "Angora, Angora rabbit"}, {326, "hamster"}, {327, "porcupine, hedgehog"}, {328, "fox squirrel, eastern fox squirrel, Sciurus niger"}, {329, "marmot"},
            {330, "beaver"}, {331, "guinea pig, Cavia cobaya"}, {332, "sorrel"}, {333, "zebra"}, {334, "hog, pig, grunter, swine, Sus scrofa"}, {335, "wild boar, boar, Sus scrofa"}, {336, "warthog"}, {337, "hippopotamus, hippo, river horse, Hippopotamus amphibius"}, {338, "ox"}, {339, "water buffalo, water ox, Asian buffalo, Bubalus bubalis"},
            {340, "bison"}, {341, "ram, tup"}, {342, "bighorn, bighorn sheep, cimarron, Rocky Mountain bighorn, Rocky Mountain sheep, Ovis canadensis"}, {343, "ibex, Capra ibex"}, {344, "hartebeest"}, {345, "impala, Aepyceros melampus"}, {346, "gazelle"}, {347, "Arabian camel, dromedary, Arabian one-humped camel, Camelus dromedarius"}, {348, "llama"}, {349, "weasel"},
            {350, "mink"}, {351, "polecat, fitch, foulmart, foumart, Mustela putorius"}, {352, "black-footed ferret, ferret, Mustela nigripes"}, {353, "otter"}, {354, "skunk, polecat, wood pussy"}, {355, "badger"}, {356, "armadillo"}, {357, "three-toed sloth, ai, Bradypus tridactylus"}, {358, "orangutan, orang, orangutang, Pongo pygmaeus"}, {359, "gorilla, Gorilla gorilla"},
            {360, "chimpanzee, chimp, Pan troglodytes"}, {361, "gibbon, Hylobates lar"}, {362, "siamang, Hylobates syndactylus, Symphalangus syndactylus"}, {363, "guenon, guenon monkey"}, {364, "patas, hussar monkey, Erythrocebus patas"}, {365, "baboon"}, {366, "macaque"}, {367, "langur"}, {368, "colobus, colobus monkey"}, {369, "proboscis monkey, Nasalis larvatus"},
            {370, "marmoset"}, {371, "capuchin, ringtail, Cebus capucinus"}, {372, "howler monkey, howler"}, {373, "titi, titi monkey"}, {374, "spider monkey, Ateles geoffroyi"}, {375, "squirrel monkey, Saimiri sciureus"}, {376, "Madagascar cat, ring-tailed lemur, Lemur catta"}, {377, "indri, indris, Indri indri, Indri brevicaudatus"}, {378, "Indian elephant, Elephas maximus"}, {379, "African elephant, Loxodonta africana"},
            {380, "lesser panda, red panda, panda, bear cat, cat bear, Ailurus fulgens"}, {381, "giant panda, panda, panda bear, coon bear, Ailuropoda melanoleuca"}, {382, "barracouta, snoek"}, {383, "eel"}, {384, "coho, cohoe, coho salmon, blue jack, silver salmon, Oncorhynchus kisutch"}, {385, "rock beauty, Holocanthus tricolor"}, {386, "anemone fish"}, {387, "sturgeon"}, {388, "gar, garfish, garpike, Lepisosteus osseus"}, {389, "lionfish"},
            {390, "pufferfish, puffer, blowfish, globefish"}, {391, "abacus"}, {392, "abaya"}, {393, "academic gown, academic robe, judge's robe"}, {394, "accordion, piano accordion, squeeze box"}, {395, "acoustic guitar"}, {396, "aircraft carrier, carrier, flattop, attack aircraft carrier"}, {397, "airliner"}, {398, "airship, dirigible"}, {399, "altar"},
            {400, "ambulance"}, {401, "amphibian, amphibious vehicle"}, {402, "analog clock"}, {403, "apiary, bee house"}, {404, "apron"}, {405, "ashcan, trash can, garbage can, wastebin, ash bin, ash-bin, ashbin, dustbin, trash barrel, trash bin"}, {406, "assault rifle, assault gun"}, {407, "backpack, back pack, knapsack, packsack, rucksack, haversack"}, {408, "bakery, bakeshop, bakehouse"}, {409, "balance beam, beam"},
            {410, "balloon"}, {411, "ballpoint, ballpoint pen, ballpen, Biro"}, {412, "Band Aid"}, {413, "banjo"}, {414, "bannister, banister, balustrade, balusters, handrail"}, {415, "barbell"}, {416, "barber chair"}, {417, "barbershop"}, {418, "barn"}, {419, "barometer"},
            {420, "barrel, cask"}, {421, "barrow, garden cart, lawn cart, wheelbarrow"}, {422, "baseball"}, {423, "basketball"}, {424, "bassinet"}, {425, "bassoon"}, {426, "bathing cap, swimming cap"}, {427, "bath towel"}, {428, "bathtub, bathing tub, bath, tub"}, {429, "beach wagon, station wagon, wagon, estate car, beach waggon, station waggon, waggon"},
            {430, "beacon, lighthouse, beacon light, pharos"}, {431, "beaker"}, {432, "bearskin, busby, shako"}, {433, "beer bottle"}, {434, "beer glass"}, {435, "bell cote, bell cot"}, {436, "bib"}, {437, "bicycle-built-for-two, tandem bicycle, tandem"}, {438, "bikini, two-piece"}, {439, "binder, ring-binder"},
            {440, "binoculars, field glasses, opera glasses"}, {441, "birdhouse"}, {442, "boathouse"}, {443, "bobsled, bobsleigh, bob"}, {444, "bolo tie, bolo, bola tie, bola"}, {445, "bonnet, poke bonnet"}, {446, "bookcase"}, {447, "bookshop, bookstore, bookstall"}, {448, "bottlecap"}, {449, "bow"},
            {450, "bow tie, bow-tie, bowtie"}, {451, "brass, memorial tablet, plaque"}, {452, "brassiere, bra, bandeau"}, {453, "breakwater, groin, groyne, mole, bulwark, seawall, jetty"}, {454, "breastplate, aegis, egis"}, {455, "broom"}, {456, "bucket, pail"}, {457, "buckle"}, {458, "bulletproof vest"}, {459, "bullet train, bullet"},
            {460, "butcher shop, meat market"}, {461, "cab, hack, taxi, taxicab"}, {462, "caldron, cauldron"}, {463, "candle, taper, wax light"}, {464, "cannon"}, {465, "canoe"}, {466, "can opener, tin opener"}, {467, "cardigan"}, {468, "car mirror"}, {469, "carousel, carrousel, merry-go-round, roundabout, whirligig"},
            {470, "carpenter's kit, tool kit"}, {471, "carton"}, {472, "car wheel"}, {473, "cash machine, cash dispenser, automated teller machine, automatic teller machine, automated teller, automatic teller, ATM"}, {474, "cassette"}, {475, "cassette player"}, {476, "castle"}, {477, "catamaran"}, {478, "CD player"}, {479, "cello, violoncello"},
            {480, "cellular telephone, cellular phone, cellphone, cell, mobile phone"}, {481, "chain"}, {482, "chainlink fence"}, {483, "chain mail, ring mail, mail, chain armor, chain armour, ring armor, ring armour"}, {484, "chain saw, chainsaw"}, {485, "chest"}, {486, "chiffonier, commode"}, {487, "chime, bell, gong"}, {488, "china cabinet, china closet"}, {489, "Christmas stocking"},
            {490, "church, church building"}, {491, "cinema, movie theater, movie theatre, movie house, picture palace"}, {492, "cleaver, meat cleaver, chopper"}, {493, "cliff dwelling"}, {494, "cloak"}, {495, "clog, geta, patten, sabot"}, {496, "cocktail shaker"}, {497, "coffee mug"}, {498, "coffeepot"}, {499, "coil, spiral, volute, whorl, helix"},
            {500, "combination lock"}, {501, "computer keyboard, keypad"}, {502, "confectionery, confectionary, sweet shop, candy store"}, {503, "container ship, containership, container vessel"}, {504, "convertible"}, {505, "corkscrew, bottle screw"}, {506, "cornet, horn, trumpet, trump"}, {507, "cowboy boot"}, {508, "cowboy hat, ten-gallon hat"}, {509, "cradle"},
            {510, "crane"}, {511, "crash helmet"}, {512, "crate"}, {513, "crib, cot"}, {514, "Crock Pot"}, {515, "croquet ball"}, {516, "crutch"}, {517, "cuirass"}, {518, "dam, dike, dyke"}, {519, "desk"},
            {520, "desktop computer"}, {521, "dial telephone, dial phone"}, {522, "diaper, nappy, napkin"}, {523, "digital clock"}, {524, "digital watch"}, {525, "dining table, board"}, {526, "dishrag, dishcloth"}, {527, "dishwasher, dish washer, dishwashing machine"}, {528, "disk brake, disc brake"}, {529, "dock, dockage, docking facility"},
            {530, "dogsled, dog sled, dog sleigh"}, {531, "dome"}, {532, "doormat, welcome mat"}, {533, "drilling platform, offshore rig, oil rig, oilrig"}, {534, "drum, membranophone, tympan"}, {535, "drumstick"}, {536, "dumbbell"}, {537, "Dutch oven"}, {538, "electric fan, blower"}, {539, "electric guitar"},
            {540, "electric locomotive"}, {541, "entertainment center"}, {542, "envelope"}, {543, "espresso maker"}, {544, "face powder"}, {545, "feather boa, boa"}, {546, "file, file cabinet, filing cabinet"}, {547, "fireboat"}, {548, "fire engine, fire truck"}, {549, "fire screen, fireguard"},
            {550, "flagpole, flagstaff"}, {551, "flute, transverse flute"}, {552, "folding chair"}, {553, "football helmet"}, {554, "forklift"}, {555, "fountain"}, {556, "fountain pen"}, {557, "four-poster"}, {558, "freight car"}, {559, "French horn, horn"},
            {560, "frying pan, frypan, skillet"}, {561, "fur coat"}, {562, "garbage truck, dustcart"}, {563, "gasmask, respirator, gas helmet"}, {564, "gas pump, gasoline pump, petrol pump, island dispenser"}, {565, "goblet"}, {566, "go-kart"}, {567, "golf ball"}, {568, "golfcart, golf cart"}, {569, "gondola"},
            {570, "gong, tam-tam"}, {571, "gown"}, {572, "grand piano, grand"}, {573, "greenhouse, nursery, glasshouse"}, {574, "grille, radiator grille"}, {575, "grocery store, grocery, food market, market"}, {576, "guillotine"}, {577, "hair slide"}, {578, "hair spray"}, {579, "half track"},
            {580, "hammer"}, {581, "hamper"}, {582, "hand blower, blow dryer, blow drier, hair dryer, hair drier"}, {583, "hand-held computer, hand-held microcomputer"}, {584, "handkerchief, hankie, hanky, hankey"}, {585, "hard disc, hard disk, fixed disk"}, {586, "harmonica, mouth organ, harp, mouth harp"}, {587, "harp"}, {588, "harvester, reaper"}, {589, "hatchet"},
            {590, "holster"}, {591, "home theater, home theatre"}, {592, "horizontal bar, high bar"}, {593, "horse cart, horse-drawn vehicle"}, {594, "hourglass"}, {595, "iPod"}, {596, "iron, smoothing iron"}, {597, "jack-o'-lantern"}, {598, "jeep, landrover"}, {599, "jeep, landrover"},
            {600, "jersey, T-shirt, tee shirt"}, {601, "jigsaw puzzle"}, {602, "jinrikisha, ricksha, rickshaw"}, {603, "joystick"}, {604, "kimono"}, {605, "knee pad"}, {606, "knot"}, {607, "lab coat, laboratory coat"}, {608, "ladle"}, {609, "lampshade, lamp shade"},
            {610, "laptop, laptop computer"}, {611, "lawn mower, mower"}, {612, "lens cap, lens cover"}, {613, "letter opener, paper knife, paperknife"}, {614, "library"}, {615, "lifeboat"}, {616, "lighter, light, igniter, ignitor"}, {617, "limousine, limo"}, {618, "liner, ocean liner"}, {619, "lipstick, lip rouge"},
            {620, "Loafer"}, {621, "lotion"}, {622, "loudspeaker, speaker, speaker unit, loudspeaker system, speaker system"}, {623, "loupe, jeweler's loupe"}, {624, "lumbermill, sawmill"}, {625, "magnetic compass"}, {626, "mailbag, postbag"}, {627, "mailbox, letter box"}, {628, "maillot, tank suit"}, {629, "maillot"},
            {630, "manhole cover"}, {631, "maraca"}, {632, "marimba, xylophone"}, {633, "mask"}, {634, "matchstick"}, {635, "maypole"}, {636, "maze, labyrinth"}, {637, "measuring cup"}, {638, "medicine chest, medicine cabinet"}, {639, "megalith, megalithic structure"},
            {640, "microphone, mike"}, {641, "microwave, microwave oven"}, {642, "military uniform"}, {643, "milk can"}, {644, "mixing bowl"}, {645, "mobile home, manufactured home"}, {646, "Model T"}, {647, "modem"}, {648, "monastery"}, {649, "monitor"},
            {650, "moped"}, {651, "mortar"}, {652, "mortarboard"}, {653, "mosque"}, {654, "motor scooter, scooter"}, {655, "mountain bike, all-terrain bike, off-roader"}, {656, "mountain tent"}, {657, "mouse, computer mouse"}, {658, "mousetrap"}, {659, "moving van"},
            {660, "muzzle"}, {661, "nail"}, {662, "neck brace"}, {663, "necklace"}, {664, "nipple"}, {665, "notebook, notebook computer"}, {666, "obelisk"}, {667, "oboe, hautbois, oboe"}, {668, "ocarina, sweet potato"}, {669, "odometer, hodometer, mileometer, milometer"},
            {670, "oil filter"}, {671, "organ, pipe organ"}, {672, "oscilloscope, scope, cathode-ray oscilloscope, CRO"}, {673, "overskirt"}, {674, "oxcart"}, {675, "oxygen mask"}, {676, "packet"}, {677, "paddle, boat paddle"}, {678, "paddlewheel, paddle wheel"}, {679, "padlock"},
            {680, "paintbrush"}, {681, "pajama, pyjama, pj's, jammies"}, {682, "palace"}, {683, "panpipe, pandean pipe"}, {684, "paper towel"}, {685, "parachute, chute"}, {686, "parallel bars, bars"}, {687, "park bench"}, {688, "parking meter"}, {689, "passenger car, coach, carriage"},
            {690, "patio, terrace"}, {691, "pay-phone, pay-station"}, {692, "pedestal, plinth, footstall"}, {693, "pencil box, pencil case"}, {694, "pencil sharpener"}, {695, "perfume, essence"}, {696, "Petri dish"}, {697, "photocopier"}, {698, "pick, plectrum, plectron"}, {699, "pickelhaube"},
            {700, "picket fence, paling"}, {701, "pickup, pickup truck"}, {702, "pier"}, {703, "piggy bank, penny bank"}, {704, "pill bottle"}, {705, "pillow"}, {706, "ping-pong ball"}, {707, "pinwheel"}, {708, "pirate, pirate ship"}, {709, "pitcher, ewer"},
            {710, "plane, carpenter's plane, woodworking plane"}, {711, "planetarium"}, {712, "plastic bag"}, {713, "plate rack"}, {714, "plow, plough"}, {715, "plunger, plumber's helper"}, {716, "Polaroid camera, Polaroid Land camera"}, {717, "pole"}, {718, "police van, police wagon, paddy wagon, patrol wagon, wagon, black Maria"}, {719, "poncho"},
            {720, "pool table, billiard table, snooker table"}, {721, "pop bottle, soda bottle"}, {722, "pot, flowerpot"}, {723, "potter's wheel"}, {724, "power drill"}, {725, "prayer rug, prayer mat"}, {726, "printer"}, {727, "prison, prison house"}, {728, "projectile, missile"}, {729, "projector"},
            {730, "puck, hockey puck"}, {731, "punching bag, punchbag, punching ball, punchball"}, {732, "purse"}, {733, "quill, quill pen"}, {734, "quilt, comforter, comfort, puff"}, {735, "racer, race car, racing car"}, {736, "racket, racquet"}, {737, "radiator"}, {738, "radio, wireless"}, {739, "radio telescope, radio reflector"},
            {740, "rain barrel"}, {741, "recreational vehicle, RV, R.V."}, {742, "reel"}, {743, "reflex camera"}, {744, "refrigerator, icebox"}, {745, "remote control, remote"}, {746, "restaurant, eating house, eating place, eatery"}, {747, "revolver, six-gun, six-shooter"}, {748, "rifle"}, {749, "rocking chair, rocker"},
            {750, "rotisserie"}, {751, "rubber eraser, rubber, pencil eraser"}, {752, "rugby ball"}, {753, "rule, ruler"}, {754, "running shoe"}, {755, "safe"}, {756, "safety pin"}, {757, "saltshaker, salt shaker"}, {758, "sandal"}, {759, "sarong"},
            {760, "sax, saxophone"}, {761, "scabbard"}, {762, "scale, weighing machine"}, {763, "school bus"}, {764, "schooner"}, {765, "scoreboard"}, {766, "screen, CRT screen"}, {767, "screw"}, {768, "screwdriver"}, {769, "seat belt, seatbelt"},
            {770, "sewing machine"}, {771, "shield, buckler"}, {772, "shoe shop, shoe-shop, shoe store"}, {773, "shoji"}, {774, "shopping basket"}, {775, "shopping cart"}, {776, "shovel"}, {777, "shower cap"}, {778, "shower curtain"}, {779, "ski"},
            {780, "ski mask"}, {781, "sleeping bag"}, {782, "slide rule, slipstick"}, {783, "sliding door"}, {784, "slot, one-armed bandit"}, {785, "snorkel"}, {786, "snowmobile"}, {787, "snowplow, snowplough"}, {788, "soap dispenser"}, {789, "soccer ball"},
            {790, "sock"}, {791, "solar dish, solar collector, solar furnace"}, {792, "sombrero"}, {793, "soup bowl"}, {794, "space bar"}, {795, "space heater"}, {796, "space shuttle"}, {797, "spatula"}, {798, "speedboat"}, {799, "spider web, spider's web"},
            {800, "spindle"}, {801, "sports car, sport car"}, {802, "spotlight, spot"}, {803, "stage"}, {804, "steam locomotive"}, {805, "steel arch bridge"}, {806, "steel drum"}, {807, "stethoscope"}, {808, "stole"}, {809, "stone wall"},
            {810, "stopwatch, stop watch"}, {811, "stove"}, {812, "strainer"}, {813, "streetcar, tram, tramcar, trolley, trolley car"}, {814, "stretcher"}, {815, "studio couch, day bed"}, {816, "stupa, tope"}, {817, "submarine, pigboat, sub, U-boat"}, {818, "suit, suit of clothes"}, {819, "sundial"},
            {820, "sunglass"}, {821, "sunglasses, dark glasses, shades"}, {822, "sunscreen, sunblock, sun blocker"}, {823, "suspension bridge"}, {824, "swab, swob, mop"}, {825, "sweatshirt"}, {826, "swimming trunks, bathing trunks"}, {827, "swing"}, {828, "switch, electric switch, electrical switch"}, {829, "syringe"},
            {830, "table lamp"}, {831, "tank, army tank, armored combat vehicle, armoured combat vehicle"}, {832, "tape player"}, {833, "teapot"}, {834, "teddy, teddy bear"}, {835, "television, television system"}, {836, "tennis ball"}, {837, "thatch, thatched roof"}, {838, "theater curtain, theatre curtain"}, {839, "thimble"},
            {840, "thresher, thrasher, threshing machine"}, {841, "throne"}, {842, "thumb tack, pushpin, drawing pin"}, {843, "tiara, diadem"}, {844, "tibetan terrier, chrysanthemum dog"}, {845, "tippet"}, {846, "toaster, toaster oven"}, {847, "tobacco shop, tobacconist shop, tobacconist"}, {848, "toilet seat"}, {849, "torch"},
            {850, "totem pole"}, {851, "tow truck, tow car, wrecker"}, {852, "toyshop"}, {853, "tractor"}, {854, "trailer truck, tractor trailer, trucking rig, rig, articulated lorry, semi"}, {855, "tray"}, {856, "trench coat"}, {857, "tricycle, trike, velocipede"}, {858, "trimaran"}, {859, "tripod"},
            {860, "triumphal arch"}, {861, "trolleybus, trolley coach, trackless trolley"}, {862, "trombone"}, {863, "tub, vat"}, {864, "turnstile"}, {865, "typing keyboard"}, {866, "umbrella"}, {867, "unicycle, monocycle"}, {868, "upright, upright piano"}, {869, "vacuum, vacuum cleaner"},
            {870, "vase"}, {871, "vault"}, {872, "velvet"}, {873, "vending machine"}, {874, "vestment"}, {875, "viaduct"}, {876, "violin, fiddle"}, {877, "volleyball"}, {878, "waffle iron"}, {879, "wall clock"},
            {880, "wallet, billfold, notecase, pocketbook"}, {881, "wardrobe, closet, press"}, {882, "warplane, military plane"}, {883, "washbasin, handbasin, washbowl, lavabo, wash-hand basin"}, {884, "washer, automatic washer, washing machine"}, {885, "water bottle"}, {886, "water jug"}, {887, "water tower"}, {888, "whiskey jug"}, {889, "whistle"},
            {890, "wig"}, {891, "window screen"}, {892, "window shade"}, {893, "Windsor tie"}, {894, "wine bottle"}, {895, "wing"}, {896, "wok"}, {897, "wooden spoon"}, {898, "wool, woolen, woollen"}, {899, "worm fence, snake fence, snake-rail fence, Virginia fence"},
            {900, "wreck"}, {901, "yawl"}, {902, "yurt"}, {903, "web site, website, internet site, site"}, {904, "comic book"}, {905, "crossword puzzle, crossword"}, {906, "street sign"}, {907, "traffic light, traffic signal, stoplight"}, {908, "book jacket, dust cover, dust jacket, dust wrapper"}, {909, "menu"},
            {910, "plate"}, {911, "guacamole"}, {912, "consomme"}, {913, "hot pot, hotpot"}, {914, "trifle"}, {915, "ice cream, icecream"}, {916, "ice lolly, lolly, lollipop, popsicle"}, {917, "French loaf"}, {918, "bagel, beigel"}, {919, "pretzel"},
            {920, "cheeseburger"}, {921, "hotdog, hot dog, red hot"}, {922, "mashed potato"}, {923, "head cabbage"}, {924, "broccoli"}, {925, "cauliflower"}, {926, "zucchini, courgette"}, {927, "spaghetti squash"}, {928, "acorn squash"}, {929, "butternut squash"},
            {930, "cucumber, cuke"}, {931, "artichoke, globe artichoke"}, {932, "bell pepper"}, {933, "cardoon"}, {934, "mushroom"}, {935, "Granny Smith"}, {936, "strawberry"}, {937, "orange"}, {938, "lemon"}, {939, "fig"},
            {940, "pineapple, ananas"}, {941, "banana"}, {942, "jackfruit, jak, jack"}, {943, "custard apple"}, {944, "pomegranate"}, {945, "hay"}, {946, "carbonara"}, {947, "chocolate sauce, chocolate syrup"}, {948, "dough"}, {949, "meat loaf, meatloaf"},
            {950, "pizza, pizza pie"}, {951, "potpie"}, {952, "burrito"}, {953, "red wine"}, {954, "espresso"}, {955, "cup"}, {956, "eggnog"}, {957, "alp"}, {958, "bubble"}, {959, "cliff, drop, drop-off"},
            {960, "coral reef"}, {961, "geyser"}, {962, "lakeside, lakeshore"}, {963, "promontory, headland, head, foreland"}, {964, "sandbar, sand bar"}, {965, "seashore, coast, seacoast, sea-coast"}, {966, "valley, vale"}, {967, "volcano"}, {968, "ballplayer, baseball player"}, {969, "groom, bridegroom"},
            {970, "scuba diver"}, {971, "rapeseed"}, {972, "daisy"}, {973, "yellow lady's slipper, yellow lady-slipper, Cypripedium calceolus, Cypripedium parviflorum"}, {974, "corn"}, {975, "acorn"}, {976, "hip, rose hip, rosehip"}, {977, "buckeye, horse chestnut, conker"}, {978, "coral fungus"}, {979, "agaric"},
            {980, "gyromitra"}, {981, "stinkhorn, carrion fungus"}, {982, "earthstar"}, {983, "hen-of-the-woods, hen of the woods, Polyporus frondosus, Grifola frondosa"}, {984, "bolete"}, {985, "ear, spike, capitulum"}, {986, "toilet tissue, toilet paper, bathroom tissue"}
        };
        /// <summary>
        /// CIFAR-100 数据集的 100 个类别名称映射。
        /// Key: 类别ID (从0开始)
        /// Value: 类别名称
        /// </summary>
        public static readonly Dictionary<int, string> Cifar100ClassNames = new Dictionary<int, string>
        {
            { 0, "apple" }, { 1, "aquarium_fish" }, { 2, "baby" }, { 3, "bear" }, { 4, "beaver" },
            { 5, "bed" }, { 6, "bee" }, { 7, "beetle" }, { 8, "bicycle" }, { 9, "bottle" },
            { 10, "bowl" }, { 11, "boy" }, { 12, "bridge" }, { 13, "bus" }, { 14, "butterfly" },
            { 15, "camel" }, { 16, "can" }, { 17, "castle" }, { 18, "caterpillar" }, { 19, "cattle" },
            { 20, "chair" }, { 21, "chimpanzee" }, { 22, "clock" }, { 23, "cloud" }, { 24, "cockroach" },
            { 25, "couch" }, { 26, "crab" }, { 27, "crocodile" }, { 28, "cruise_ship" }, { 29, "cup" },
            { 30, "dinosaur" }, { 31, "dolphin" }, { 32, "elephant" }, { 33, "flatfish" }, { 34, "forest" },
            { 35, "fox" }, { 36, "girl" }, { 37, "hamster" }, { 38, "house" }, { 39, "kangaroo" },
            { 40, "computer_keyboard" }, { 41, "lamp" }, { 42, "lawn_mower" }, { 43, "leopard" }, { 44, "lion" },
            { 45, "lizard" }, { 46, "lobster" }, { 47, "man" }, { 48, "maple_tree" }, { 49, "motorcycle" },
            { 50, "mountain" }, { 51, "mouse" }, { 52, "mushroom" }, { 53, "oak_tree" }, { 54, "orange" },
            { 55, "orchid" }, { 56, "otter" }, { 57, "palm_tree" }, { 58, "pear" }, { 59, "pickup_truck" },
            { 60, "pine_tree" }, { 61, "plain" }, { 62, "plate" }, { 63, "poppy" }, { 64, "porcupine" },
            { 65, "possum" }, { 66, "rabbit" }, { 67, "raccoon" }, { 68, "ray" }, { 69, "road" },
            { 70, "rocket" }, { 71, "rose" }, { 72, "sea" }, { 73, "seal" }, { 74, "shark" },
            { 75, "shrew" }, { 76, "skunk" }, { 77, "skyscraper" }, { 78, "snail" }, { 79, "snake" },
            { 80, "spider" }, { 81, "squirrel" }, { 82, "streetcar" }, { 83, "sunflower" }, { 84, "sweet_pepper" },
            { 85, "table" }, { 86, "tank" }, { 87, "telephone" }, { 88, "television" }, { 89, "tiger" },
            { 90, "tractor" }, { 91, "train" }, { 92, "trout" }, { 93, "tulip" }, { 94, "turtle" },
            { 95, "wardrobe" }, { 96, "whale" }, { 97, "willow_tree" }, { 98, "wolf" }, { 99, "woman" }
        };


    }
}
